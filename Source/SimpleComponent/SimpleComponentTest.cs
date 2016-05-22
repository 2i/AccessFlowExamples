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

namespace SimpleComponent
{
    internal class SimpleComponentTest : Test
    {
        public override void Run()
        {
            run().Wait();
        }

        #region private
        private static async Task run()
        {
            Console.WriteLine("\nasynchronous test");
            DateTime start = DateTime.Now;
            await TaskCollector.With(tc =>
            {
                using (var comp = new MyComponent("test component 1"))
                {
                    tc.Add(comp.DoC("t0"));
                    tc.Add(comp.DoA("t1"));
                    tc.Add(comp.DoB("t2"));
                    tc.Add(comp.DoCombined("t3"));
                    tc.Add(comp.DoB("t4"));
                    tc.Add(comp.DoC("t5"));
                }
            });
            Console.WriteLine($"duration: {DateTime.Now - start}");

            Console.WriteLine("\nsynchronous test");
            start = DateTime.Now;
            using (var comp = new MyComponent("test component 2"))
            {
                await comp.DoC("t0");
                await comp.DoA("t1");
                await comp.DoB("t2");
                await comp.DoCombinedSync("t3");
                await comp.DoB("t4");
                await comp.DoC("t5");
            }
            Console.WriteLine($"duration: {DateTime.Now - start}");
        }
        #endregion
    }
}
