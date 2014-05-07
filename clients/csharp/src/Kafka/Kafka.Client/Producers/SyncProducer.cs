﻿namespace Kafka.Client.Producers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;

    using Kafka.Client.Api;
    using Kafka.Client.Cfg;
    using Kafka.Client.Network;

    using log4net;

    internal class SyncProducer : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const short RequestKey = 0;

        public readonly Random RandomGenerator = new Random();

        private object @lock = new object();

        private bool shutdown = false;

        private BlockingChannel blockingChannel;

        public string BrokerInfo { get; private set; }

        public SyncProducerConfiguration Config { get; private set; }

        private ProducerRequestStats producerRequestStats;

        public SyncProducer(SyncProducerConfiguration config)
        {
            Logger.Debug("Instantiating Scala Sync Producer");

            this.Config = config;
            this.blockingChannel = new BlockingChannel(config.Host, config.Port, BlockingChannel.UseDefaultBufferSize, config.SendBufferBytes, config.RequestTimeoutMs);
            this.BrokerInfo = string.Format("host_{0}-port_{1}", config.Host, config.Port);
            this.producerRequestStats = ProducerRequestStatsRegistry.GetProducerRequestStats(config.ClientId);
        }

        private void VerifyRequest(RequestOrResponse request)
        {
            /**
             * This seems a little convoluted, but the idea is to turn on verification simply changing log4j settings
             * Also, when verification is turned on, care should be taken to see that the logs don't fill up with unnecessary
             * Data. So, leaving the rest of the logging at TRACE, while errors should be logged at ERROR level
             */
            if (Logger.IsDebugEnabled)
            {
                var buffer = new BoundedByteBufferSend(request).Buffer;
                Logger.Debug("Verifying sendbuffer of size " + buffer.Limit());
                var requestTypeId = buffer.GetShort();
                if (requestTypeId == RequestKeys.ProduceKey)
                {
                    var innerRequest = ProducerRequest.ReadFrom(buffer);
                    Logger.Debug(innerRequest.ToString());
                }
            }

        }

        public Receive DoSend(RequestOrResponse request, bool readResponse = true)
        {
            lock (@lock)
            {
                this.VerifyRequest(request);
                this.GetOrMakeConnection();

                Receive response = null;
                try
                {
                    blockingChannel.Send(request);
                    if (readResponse)
                    {
                        response = this.blockingChannel.Receive();
                    }
                    else
                    {
                        Logger.Debug("Skipping reading response");
                    }
                }
                catch (IOException e)
                {
                    // no way to tell if write succeeded. Disconnect and re-throw exception to let client handle retry
                    this.Disconnect();
                    throw e;
                }
                return response;
            }
        }

        public ProducerResponse Send(ProducerRequest producerRequest)
        {
            var requestSize = producerRequest.SizeInBytes;
            producerRequestStats.GetProducerRequestStats(BrokerInfo).RequestSizeHist.Update(requestSize);
            producerRequestStats.GetProducerRequestAllBrokersStats().RequestSizeHist.Update(requestSize);

            Receive response = null;
            var specificTimer = producerRequestStats.GetProducerRequestStats(BrokerInfo).RequestTimer;
            var aggregateTimer = producerRequestStats.GetProducerRequestAllBrokersStats().RequestTimer;

            aggregateTimer.Time(() => specificTimer.Time(() =>
                {
                    response = this.DoSend(producerRequest, producerRequest.RequiredAcks != 0);
                }));

            if (producerRequest.RequiredAcks != 0)
            {
                return ProducerResponse.ReadFrom(response.Buffer);
            }
            else
            {
                return null;
            }
        }

        public TopicMetadataResponse Send(TopicMetadataRequest request)
        {
            var response = this.DoSend(request);
            return TopicMetadataResponse.ReadFrom(response.Buffer);
        }

        public void Dispose()
        {
            lock (@lock)
            {
                this.Disconnect();
                shutdown = true;
            }
        }

        /// <summary>
        /// Disconnect from current channel, closing connection.
        /// Side effect: channel field is set to null on successful disconnect
        /// </summary>
        private void Disconnect()
        {
            try
            {
                if (this.blockingChannel.IsConnected)
                {
                    Logger.InfoFormat("Disconnecting from {0}:{1}", Config.Host, Config.Port);
                    this.blockingChannel.Disconnect();
                }
            } 
            catch (Exception e) 
            {
                Logger.ErrorFormat("Error on disconnect", e);
            }
        }

        private BlockingChannel Connect()
        {
            if (!this.blockingChannel.IsConnected && !shutdown)
            {
                try
                {
                    this.blockingChannel.Connect();
                    Logger.InfoFormat("Connected to {0}:{1} for producing", Config.Host, Config.Port);
                }
                catch (Exception e)
                {
                    this.Disconnect();
                    Logger.ErrorFormat("Producer connection to {0}:{1} unsuccessful", Config.Host, Config.Port, e);
                    throw e;
                }
            }
            return this.blockingChannel;
        }

        private void GetOrMakeConnection()
        {
            if (!this.blockingChannel.IsConnected)
            {
                this.Connect();
            }
        }


    }
}