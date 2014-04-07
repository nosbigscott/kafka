﻿using System;

namespace Kafka.Client.Common
{
    /// <summary>
    ///  Generic Kafka exception
    /// </summary>
    public class KafkaException : Exception
    {
        public KafkaException()
            : base()
        {
        }

        public KafkaException(string message)
            : base(message)
        {
        }

        public KafkaException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}