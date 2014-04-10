﻿namespace Kafka.Client.Cluster
{
    using System;
    using System.IO;

    using Kafka.Client.Api;
    using Kafka.Client.Extensions;

    public class Broker
    {
        public int Id { get; private set; }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public Broker(int id, string host, int port)
        {
            this.Id = id;
            this.Host = host;
            this.Port = port;
        }

        public override string ToString()
        {
            return string.Format("Id: {0}, Host: {1}, Port: {2}", this.Id, this.Host, this.Port);
        }

        public string GetConnectionString()
        {
            return this.Host + ":" + this.Port;
        }

        public void WriteTo(MemoryStream buffer)
        {
            buffer.PutInt(this.Id);
            ApiUtils.WriteShortString(buffer, this.Host);
            buffer.PutInt(this.Port);
        }

        public int SizeInBytes 
        { 
            get
            {
                return ApiUtils.ShortStringLength(this.Host) /* host name */ + 4 /* port */ + 4 /* broker id*/;
            }
        }

        public static Broker CreateBroker(int id, string brokerInfoString)
        {
            throw new NotImplementedException();
        }

        protected bool Equals(Broker other)
        {
            return this.Id == other.Id && string.Equals(this.Host, other.Host) && this.Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == this.GetType() && this.Equals((Broker)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = this.Id;
                hashCode = (hashCode * 397) ^ (this.Host != null ? this.Host.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ this.Port;
                return hashCode;
            }
        }

        internal static Broker ReadFrom(MemoryStream buffer)
        {
            var id = buffer.GetInt();
            var host = ApiUtils.ReadShortString(buffer);
            var port = buffer.GetInt();
            return new Broker(id, host, port);
        }
    }
}