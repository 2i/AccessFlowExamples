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
using AccessFlow.BasicScopes;

namespace AsyncBoxes
{
    public static class AsyncBox
    {
        public static IAsyncBox<T> Create<T>(T initial)
        {
            return new asyncBox<T>(initial);
        }

        #region private
        private class asyncBox<T> :
            AccessContextual<RWScope, asyncBox<T>, IAsyncBox<T>>, IAsyncBox<T>
        {
            public IReactive<T> Get()
            {
                Task<T> process = Context.InChild(RWScope.Read,
                    async context =>
                {
                    await context.UntilAvailable(RWScope.Read);
                    return state[0];
                });
                return Reactive.Create(process, TaskFuture.Create(process));
            }

            public async Task Set(IFuture<T> value)
            {
                await Context.InChild(RWScope.ReadWrite, async context =>
                {
                    await context.UntilAvailable(RWScope.ReadWrite);
                    state[0] = await value;
                });
            }

            public override asyncBox<T> ThisAsTParent()
            {
                return this;
            }

            public asyncBox(T initial)
                : base(AccessContext.Create(
                    RWScopeLogic.Instance,
                    RWScopeLogic.MaximumScope),
                    childFactory)
            {
                state = new[] { initial };
            }

            #region private
            private readonly T[] state;

            IAsyncBox<T> IAccessContextual<RWScope, IAsyncBox<T>, IAsyncBox<T>>
                .ThisAsTParent()
            {
                return this;
            }

            private asyncBox(
                asyncBox<T> parent, IAccessContext<RWScope> context)
                : base(context, childFactory)
            {
                state = parent.state;
            }

            private static readonly IChildContextualFactory<
                RWScope, asyncBox<T>, IAsyncBox<T>>
                childFactory = LambdaSubContextualFactory.Create<
                    RWScope, asyncBox<T>, IAsyncBox<T>>(
                        (parent, childContext) =>
                            new asyncBox<T>(parent, childContext));
            #endregion
        }
        #endregion
    }
}
