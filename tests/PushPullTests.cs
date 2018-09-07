using nng.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace nng.Tests
{
    using static nng.Native.Aio.UnsafeNativeMethods;
    using static nng.Native.Msg.UnsafeNativeMethods;
    using static nng.Tests.Util;

    public class PushPullTests
    {
        TestFactory factory = new TestFactory();

        [Fact]
        public async Task PushPull()
        {
            var url = UrlRandomIpc();
            var barrier = new AsyncBarrier(2);
            var push = Task.Run(async () => {
                var pushSocket = factory.CreatePusher(url, true);
                await barrier.SignalAndWait();
                Assert.True(await pushSocket.Send(factory.CreateMsg()));
            });
            var pull = Task.Run(async () => {
                await barrier.SignalAndWait();
                var pullSocket = factory.CreatePuller(url, false);
                await pullSocket.Receive(CancellationToken.None);
            });
            
            await AssertWait(1000, pull, push);
        }

        [Fact]
        public async Task Broker()
        {
            await PushPullBrokerAsync(2, 3, 2);
        }

        async Task PushPullBrokerAsync(int numPushers, int numPullers, int numMessagesPerPusher, int msTimeout = 1000)
        {
            // In pull/push (pipeline) pattern, each message is sent to one receiver in round-robin fashion
            int numTotalMessages = numPushers * numMessagesPerPusher;
            var counter = new AsyncCountdownEvent(numTotalMessages);
            var cts = new CancellationTokenSource();
            
            var broker = new Broker(new PushPullBrokerImpl(factory));
            var tasks = await broker.RunAsync(numPushers, numPullers, numMessagesPerPusher, counter, cts.Token);

            await AssertWait(msTimeout, counter.WaitAsync());
            await CancelAndWait(cts, tasks.ToArray());
        }
    }

    class PushPullBrokerImpl : IBrokerImpl<NngMessage>
    {
        public TestFactory Factory { get; private set; }

        public PushPullBrokerImpl(TestFactory factory)
        {
            Factory = factory;
        }

        public IReceiveAsyncContext<NngMessage> CreateInSocket(string url)
        {
            return Factory.CreatePuller(url, true);
        }
        public ISendAsyncContext<NngMessage> CreateOutSocket(string url)
        {
            return Factory.CreatePusher(url, true);
        }
        public IReceiveAsyncContext<NngMessage> CreateClient(string url)
        {
            return Factory.CreatePuller(url, false);
        }
    }
}