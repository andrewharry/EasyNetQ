using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyNetQ.Loggers;
using EasyNetQ.Tests.SubscriptionRPC.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class TestRequest {
    public int SenderId { get; set; }
}

public class TestResponse {
    public int SenderId { get; set; }
}

namespace EasyNetQ.Tests.SubscriptionRPC 
{
    [TestClass]
    public class SubscriptionTests 
    {
        [TestMethod]
        public async Task TestMethod1() {
            var endpoint = "unit-test-queue-1";

            var bus = RabbitHutch.CreateBus("host=localhost;persistentMessages=false;prefetchcount=30;timeout=20", service => {
                service.Register<ITypeNameSerializer, NameSerialiser>();
                service.Register<IEasyNetQLogger>(_ => new ConsoleLogger { Debug = true, Info = true, Error = true });
            });

            IDisposable token = null;

            try {
                //Setup first subscriber
                token = bus.RespondAsync(endpoint, (TestRequest v) => Handler(v, 123));
                var result = await bus.RequestAsync<TestResponse>(endpoint, new TestRequest { SenderId = 2 }, TimeSpan.FromSeconds(5), null);

                Assert.IsNotNull(result);
                Assert.AreEqual(123, result.SenderId);
            }
            finally {
                bus.Dispose();
                token.Dispose();
            }
        }

        [TestMethod]
        public async Task TestMethod2() {
            var endpoint = "unit-test-queue-2";
            var topic1 = "topic.different";
            var topic2 = "topic.another";

            var bus = RabbitHutch.CreateBus("host=localhost;persistentMessages=false;prefetchcount=30;timeout=20", service => {
                service.Register<ITypeNameSerializer, NameSerialiser>();
                service.Register<IEasyNetQLogger>(_ => new ConsoleLogger { Debug = true, Info = true, Error = true });
            });

            var token = new List<IDisposable>();

            try {
                token.Add(bus.RespondAsync(endpoint, (TestRequest v) => Handler(v, 1), Guid.NewGuid().ToString(), config => config.WithAutoDelete().WithTopic(topic2)));
                token.Add(bus.RespondAsync(endpoint, (TestRequest v) => Handler(v, 2), Guid.NewGuid().ToString(), config => config.WithAutoDelete().WithTopic(topic1)));
                var result = await bus.RequestAsync<TestResponse>(endpoint, new TestRequest { SenderId = 3 }, TimeSpan.FromSeconds(5), topic1);

                Assert.IsNotNull(result);
                Assert.AreEqual(2, result.SenderId);
            }
            finally {
                bus.Dispose();
                token.Each(v => v.Dispose());
            }
        }

        private Task<TestResponse> Handler(TestRequest request, int responseId) {
            Console.WriteLine("Received Request from {0}", request.SenderId);
            Console.WriteLine("Returning Response from {0}", responseId);
            return Task.FromResult(new TestResponse() { SenderId = responseId });
        }
    }
}
