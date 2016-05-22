#region copyright
// Copyright 2016 Christoph Müller
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AccessFlow;
using AccessFlow.BasicScopes;
using TestRunners;

namespace StreamProcessing
{
    internal class StreamProcessingTest : Test
    {
        public override void Run()
        {
            RunAsync().Wait();
        }

        public static async Task RunAsync()
        {
            await SimpleTest();
            await FibonacciTest();
        }

        public static async Task SimpleTest()
        {
            await TaskCollector.With(async tasks =>
            {
                ContextualStream<int> stream = new ContextualStream<int>();

                tasks.Add(stream.Add(3));
                AssertEquals(await stream.Get(0), 3);
                tasks.Add(stream.Add(5));
                tasks.Add(stream.Add(7));
                AssertEquals(stream.Get(2).Result, 7);
                AssertEquals(stream.Get(0).Result, 3);
                await PrintValues(stream);
            });
        }

        public static async Task FibonacciTest()
        {
            await TaskCollector.With(tasks =>
            {
                Console.WriteLine("Fibonacci:");
                var fiboNums = new ContextualStream<BigInteger>();
                tasks.Add(addFibonacciNumbers(fiboNums));
                tasks.Add(PrintValues(fiboNums));
            });
        }

        public static void AssertEquals<T>(T actual, T should)
        {
            Debug.Assert(actual.Equals(should));
            Console.WriteLine($"{actual} == {should}");
        }

        public static async Task PrintValues<T>(IChildContextualCreator<
            CSScope, ContextualStream<T>> subContextFactory)
        {
            await subContextFactory.InChild(new CSScope(valueScope:
                InfiniteIntervalScope.Create(RWScope.Read, 0, null)),
                async ints =>
                {
                    await TaskCollector.With(async tc =>
                    {
                        IAsyncEnumerator<T> enumerator =
                            ints.GetAsyncEnumerator();
                        while (await tc.Adding(enumerator.MoveNextAsync()))
                        {
                            T value =
                                await tc.Adding(enumerator.GetCurrentAsync());
                            Console.WriteLine(value);
                        }
                    });
                });
        }

        #region private
        private static async Task addFibonacciNumbers(
            ContextualStream<BigInteger> stream, int count = 30)
        {
            await TaskCollector.With(async tc =>
            {
                await stream.InChild(new CSScope(
                    RWScope.ReadWrite,
                    InfiniteIntervalScope.Create(
                        RWScope.ReadWrite, 0, null)),
                    async childStream =>
                    {
                        await Task.Yield();

                        IAppender<BigInteger> appender =
                            childStream.GetAppender();

                        BigInteger a = 1, b = 1;
                        tc.Add(appender.Append(a));
                        /* FIXME: Users of appender generally don’t know
                         * the internals of ContextualStream.
                         * Poss. solution: IAppender.MaxRequiredScope.
                         * Then it would also be useful, if (the) access scopes
                         * could be merged. */
                        childStream.Context.RequiredScope = new CSScope(
                            RWScope.ReadWrite,
                            InfiniteIntervalScope.Create(
                                RWScope.ReadWrite, 1, null));
                        tc.Add(appender.Append(b));
                        childStream.Context.RequiredScope = new CSScope(
                            RWScope.ReadWrite,
                            InfiniteIntervalScope.Create(
                                RWScope.ReadWrite, 2, null));
                        for (int i = 2; i < count; i++)
                        {
                            BigInteger c = a + b;
                            await Task.Delay(250);
                            a = b;
                            b = c;
                            tc.Add(appender.Append(c));
                            childStream.Context.RequiredScope = new CSScope(
                                RWScope.ReadWrite,
                                InfiniteIntervalScope.Create(
                                    RWScope.ReadWrite, i + 1, null));
                        }
                    });
            });
        }
        #endregion
    }
}
