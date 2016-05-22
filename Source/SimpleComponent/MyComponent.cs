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
using System.Threading;
using System.Threading.Tasks;
using AccessFlow;

namespace SimpleComponent
{
    internal class MyComponent :
        AccessContextual<long, MyComponent, MyComponent>
    {
        public async Task DoA(string name)
        {
            Console.WriteLine($"{name} created");

            // calculate accessScope
            long accessScope = 1;

            using (IAccessContext<long> q =
                Context.CreateChild(accessScope))
            {
                char contextName = NewContextName();
                
                await q.UntilAvailable();

                await Task.Run(() =>
                {
                    // perform something here
                    for (int i = 0; i < 7; i++)
                        work(q, name, contextName, "DoA");
                });
            }
        }

        public async Task DoB(string name)
        {
            Console.WriteLine($"{name} created");

            // calculate accessScope
            long accessScope = 2;

            using (IAccessContext<long> q =
                Context.CreateChild(accessScope))
            {
                char contextName = NewContextName();

                await q.UntilAvailable();

                await Task.Run(() =>
                {
                    // perform something here
                    for (int i = 0; i < 5; i++)
                        work(q, name, contextName, "DoB");
                });
            }
        }

        public async Task DoC(string name)
        {
            Console.WriteLine($"{name} created");

            // calculate accessScope
            long accessScope = 3;

            using (IAccessContext<long> q
                = Context.CreateChild(accessScope))
            {
                char contextName = NewContextName();

                await q.UntilAvailable();

                await Task.Run(() =>
                {
                    for (int i = 0; i < 5; i++)
                        work(q, name, contextName, "DoC");

                    q.RequiredScope = 1;

                    // perform something here
                    for (int i = 0; i < 6; i++)
                        work(q, name, contextName, "DoC");
                });
            }
        }

        public override MyComponent ThisAsTParent()
        {
            return this;
        }

        public MyComponent(string connectionName)
            : base(AccessContext.Create(
                new FlagBasedAccessScopeLogic(),
                FlagBasedAccessScopeLogic.FullScope),
                factory.Default)
        {
            connection = message => Console.WriteLine(
                connectionName + ": " + message);
        }

        public static string ScopeToString(long scope, char operationName)
        {
            char[] chars = new char[64];
            for (int i = 0; i < 64; i++)
            {
                if ((scope & 1L) == 1L)
                    chars[63 - i] = operationName;
                else
                    chars[63 - i] = '-';
                scope >>= 1;
            }
            return new string(chars);
        }

        public static char NewContextName()
        {
            int intName = Interlocked.Increment(ref nextComponentName) - 1;
            return (char) ('a' + intName % 26);
        }

        #region private
        private readonly Action<string> connection;

        private void work(
            IAccessContext<long> context, string name, char contextName,
            string methodname)
        {
            Thread.Sleep(250);
            connection(ScopeToString(context.RequiredScope, contextName)
                + " " + name + " " + methodname);
        }

        private MyComponent(
            Action<string> connection, IAccessContext<long> context)
            : base(context, factory.Default)
        {
            this.connection = connection;
        }

        private static int nextComponentName = 0;

        private class factory : IChildContextualFactory<
            long, MyComponent, MyComponent>
        {
            public MyComponent Create(MyComponent parentContextual,
                IAccessContext<long> childContext)
            {
                return new MyComponent(
                    parentContextual.connection, childContext);
            }

            public static readonly factory Default = new factory();
        }
        #endregion
    }
}
