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

namespace SimpleComponent
{
    internal static class MyComponentContextAdditions
    {
        public static Task DoCombined(this MyComponent parent,
            string name)
        {
            // calculate access Scope
            long scope = 3;

            return TaskCollector.With(tc =>
            {
                parent.InChild(scope, component =>
                {
                    tc.Add(component.DoA(name + ".1"));
                    tc.Add(component.DoB(name + ".2"));
                });
            });
        }

        public static async Task DoCombinedSync(this MyComponent parent,
            string name)
        {
            // calculate access Scope
            long scope = 3;

            await parent.InChild(scope, async component =>
            {
                await component.DoA(name + ".1");
                await component.DoB(name + ".2");
            });
        }
    }
}
