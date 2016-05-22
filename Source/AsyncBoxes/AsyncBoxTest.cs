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
using System.Linq;
using System.Threading.Tasks;
using AccessFlow;
using TestRunners;

namespace AsyncBoxes
{
    internal class AsyncBoxTest : Test
    {
        public override void Run()
        {
            RunAsync().Wait();
        }

        public static async Task RunAsync()
        {
            await TaskCollector.With(tc =>
            {
                IAsyncBox<int> box = AsyncBox.Create(2);
                Console.WriteLine(tc.Adding(box.Get()).Value);
                tc.Add(box.Set(3.ToFuture()));
                Console.WriteLine(tc.Adding(box.Get()).Value);
                tc.Add(Add3(box));
                Console.WriteLine(tc.Adding(box.Get()).Value);
                tc.Add(DoSet(box, 4.ToFuture()));
                Console.WriteLine(tc.Adding(box.Get()).Value);
            });
        }

        public static async Task DoSet(
            IAsyncSetter<int> setter, IFuture<int> value)
        {
            await setter.Set(value);
        }

        public static async Task Add3(IAsyncBox<int> box)
        {
            await TaskCollector.With(async tc =>
            {
                int value = await tc.Adding(box.Get());
                await box.Set((value + 3).ToFuture());
            });
        }
    }
}
