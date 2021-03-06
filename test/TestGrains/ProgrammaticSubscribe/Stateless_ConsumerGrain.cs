﻿using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using Orleans.Streams;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitTests.GrainInterfaces;
using System.Collections.Concurrent;
using Microsoft.FSharp.Collections;
using Orleans.Providers;
using Orleans.Streams.Core;
using UnitTests.Grains.ProgrammaticSubscribe;

namespace UnitTests.Grains
{
    [StatelessWorker(MaxLocalWorkers)]
    public class Stateless_ConsumerGrain : Grain, IStateless_ConsumerGrain
    {
        public const int MaxLocalWorkers = 2;
        internal Logger logger;
        private List<ConsumerObserver<int>> consumerObservers;
        private List<StreamSubscriptionHandle<int>> consumerHandles;
        private List<ConsumerObserver<string>> consumerObservers2;
        private List<StreamSubscriptionHandle<string>> consumerHandles2;
        private int onAddCalledCount;

        public override async Task OnActivateAsync()
        {
            logger = base.GetLogger("Stateless_ConsumerGrain " + base.IdentityString);
            logger.Info("OnActivateAsync");
            onAddCalledCount = 0;
            consumerObservers = new List<ConsumerObserver<int>>();
            consumerHandles = new List<StreamSubscriptionHandle<int>>();
            consumerObservers2 = new List<ConsumerObserver<string>>();
            consumerHandles2 = new List<StreamSubscriptionHandle<string>>();
            IStreamProvider streamProvider = base.GetStreamProvider("SMSProvider");
            await streamProvider.SetOnSubscriptionChangeAction<int>(this.OnAdd);
            await streamProvider.SetOnSubscriptionChangeAction<string>(this.OnAdd2);

            // adding onSubscriptionChangeAction for differnt stream provider
            streamProvider = base.GetStreamProvider("SMSProvider2");
            await streamProvider.SetOnSubscriptionChangeAction<int>(this.OnAdd);
            await streamProvider.SetOnSubscriptionChangeAction<string>(this.OnAdd2);
        }

        public Task<int> GetCountOfOnAddFuncCalled()
        {
            return Task.FromResult(this.onAddCalledCount);
        }

        public Task<int> GetNumberConsumed()
        {
            int sum = 0;
            foreach (var observer in consumerObservers)
            {
                sum += observer.NumConsumed;
            }

            foreach (var observer in consumerObservers2)
            {
                sum += observer.NumConsumed;
            }
            return Task.FromResult(sum);
        }

        public async Task StopConsuming()
        {
            logger.Info("StopConsuming");
            foreach (var handle in consumerHandles)
            {
                await handle.UnsubscribeAsync();
            }
            consumerHandles.Clear();
            consumerObservers.Clear();

            foreach (var handle in consumerHandles2)
            {
                await handle.UnsubscribeAsync();
            }
            consumerHandles2.Clear();
            consumerObservers2.Clear();
        }

        private async Task OnAdd(StreamSubscriptionHandle<int> handle)
        {
            this.onAddCalledCount++;
            var observer = new ConsumerObserver<int>(this.logger);
            var newhandle = await handle.ResumeAsync(observer);
            this.consumerObservers.Add(observer);
            this.consumerHandles.Add(newhandle);
        }

        private async Task OnAdd2(StreamSubscriptionHandle<string> handle)
        {
            var observer = new ConsumerObserver<string>(this.logger);
            var newhandle = await handle.ResumeAsync(observer);
            this.consumerObservers2.Add(observer);
            this.consumerHandles2.Add(newhandle);
        }
    }

    public class Jerk_ConsumerGrain : Grain, IJerk_ConsumerGrain
    {
        internal Logger logger;

        public override async Task OnActivateAsync()
        {
            logger = base.GetLogger("Jerk_ConsumerGrain" + base.IdentityString);
            logger.Info("OnActivateAsync");
            IStreamProvider streamProvider = base.GetStreamProvider("SMSProvider");
            await streamProvider.SetOnSubscriptionChangeAction<int>(this.OnAdd);
        }

        //Jerk_ConsumerGrai would unsubscrube on any subscription added to it
        private async Task OnAdd(StreamSubscriptionHandle<int> handle)
        {
            await handle.UnsubscribeAsync();
        }
    }


    public class ConsumerObserver<T> : IAsyncObserver<T>
    {
        public int NumConsumed { get; private set; }
        private Logger logger;
        internal ConsumerObserver(Logger logger)
        {
            this.NumConsumed = 0;
            this.logger = logger;
        }

        public Task OnNextAsync(T item, StreamSequenceToken token = null)
        {
            this.NumConsumed++;
            this.logger.Info($"Consumer {this.GetHashCode()} OnNextAsync() with NumConsumed {this.NumConsumed}");
            return TaskDone.Done;
        }

        public Task OnCompletedAsync()
        {
            this.logger.Info($"Consumer {this.GetHashCode()} OnCompletedAsync()");
            return TaskDone.Done;
        }

        public Task OnErrorAsync(Exception ex)
        {
            this.logger.Info($"Consumer {this.GetHashCode()} OnErrorAsync({ex})");
            return TaskDone.Done;
        }
    }

    [ImplicitStreamSubscription(StreamNameSpace)]
    public class ImplicitSubscribeGrain : Grain, IImplicitSubscribeGrain
    {
        public const string StreamNameSpace = "ImplicitSubscriptionSpace";
    }
}
