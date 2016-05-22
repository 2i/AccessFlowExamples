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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccessFlow;
using AccessFlow.BasicScopes;
using AsyncBoxes;

namespace StreamProcessing
{
    /// <summary>
    /// Represents an sequence or stream of values.
    /// Values may only be appended at the end of the
    /// <see cref="ContextualStream{T}"/>
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    public class ContextualStream<T> :
        AccessContextual<CSScope, ContextualStream<T>, ContextualStream<T>>,
        IAsyncEnumerable<T>
    {
        public async Task Add(T value)
        {
            await add(value, Context);
        }

        /// <summary>
        /// Returns the value at index <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Der Index des auszugebenden Wertes.</param>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when <paramref name="index"/> is greater or equal
        /// to the count of the appended elements.
        /// </exception>
        /// <returns>The value at index <paramref name="index"/>.</returns>
        public async Task<T> Get(long index)
        {
            return await Context.InChildWithin(async c =>
                (await getNodeAt(index, c)).Value);
        }

        public async Task<long> GetCount()
        {
            return await Context.InChild(
                new CSScope(RWScope.Read), getCount);
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator() =>
            new enumerator(this);

        public IEnumerator<T> GetEnumerator() => new enumerator(this);

        public IAppender<T> GetAppender()
        {
            return new appender(this);
        }

        public override ContextualStream<T> ThisAsTParent() => this;

        public ContextualStream()
            : base(AccessContext.Create(
                CsScopeLogic.Default,
                CsScopeLogic.CompleteScope),
                factory)
        {
            state = new streamData();
        }

        #region private
        private readonly streamData state;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private async Task<node> getNodeAt(
            long index, IAccessContext<CSScope> context)
        {
            return await context.InChild(
                new CSScope(valueScope: InfiniteIntervalScope.Create(
                    RWScope.Read, 0, index + 1),
                    name: $"scope for getNodeAt {index}"),
                async subContext =>
                {
                    await subContext.UntilAvailable(new CSScope(valueScope:
                        InfiniteIntervalScope.Create(RWScope.Read, 0, 1),
                        name: $"UntilAvailable scope in getNodeAt {index}"));

                    node currentNode = state.FirstNode;

                    for (long i = 1; i <= index; i++)
                    {
                        await subContext.UntilAvailable(new CSScope(
                            valueScope: InfiniteIntervalScope.Create(
                                RWScope.Read, i, i + 1),
                            name: "UntilAvailable scope in " +
                                $"getNodeAt {index}"));

                        currentNode = currentNode.Successor;
                    }

                    return currentNode;
                });
        }

        private async Task<Tuple<node, int>> getLastNode(
            IAccessContext<CSScope> context,
            node startNode = null, int startIndex = -1)
        {
            if (startNode == null)
                startIndex = -1;
            return await context.InChild(new CSScope(
                valueScope: InfiniteIntervalScope.Create(
                    RWScope.Read, startIndex + 1, null),
                name: "scope for getLastNode"),
                async subContext =>
                {
                    if (startNode == null)
                    {
                        node firstNode = await getFirstNode(subContext);
                        if (firstNode == null)
                            return null;
                        startNode = firstNode;
                        startIndex = 0;
                    }
                    var t = getLastNode(startNode, startIndex, subContext);
                    subContext.Dispose();
                    return await t;
                });
        }

        private async Task<Tuple<node, int>> getLastNode(
            node startNode, int startIndex, IAccessContext<CSScope> context)
        {
            return await context.InChild(
                new CSScope(valueScope: InfiniteIntervalScope.Create(
                    RWScope.Read, startIndex + 1, null)),
                async subContext =>
                {
                    node currentNode = startNode;
                    for (int index = startIndex + 1; ; index++)
                    {
                        /* read the node at position index */

                        await subContext.UntilAvailable(new CSScope(valueScope:
                            InfiniteIntervalScope.Create(
                                RWScope.Read, index, index + 1)));

                        if (currentNode.Successor == null)
                            return Tuple.Create(currentNode, index - 1);
                        currentNode = currentNode.Successor;
                        subContext.RequiredScope = new CSScope(valueScope:
                            InfiniteIntervalScope.Create(
                                RWScope.Read, index + 1, null));
                    }
                });
        }

        /* Private methods may get the surrounding context to spare
         * the creation of a new Contextual instance. */

        /// <remarks>
        /// No child context of <paramref name="context"/>
        /// may be created und employed before
        /// the invocation of this method because
        /// this method itself does not create an own child context.
        /// </remarks>
        private async Task<long> getCount(IAccessContext<CSScope> context)
        {
            await context.UntilAvailable(new CSScope(RWScope.Read));
            return state.Count;
        }

        /// <remarks>
        /// No child context of <paramref name="context"/>
        /// may be created und employed before
        /// the invocation of this method because
        /// this method itself does not create an own child context.
        /// </remarks>
        private async Task<node> getFirstNode(IAccessContext<CSScope> context)
        {
            await context.UntilAvailable(new CSScope(
                valueScope: InfiniteIntervalScope.Create(
                    RWScope.Read, 0, 1)));
            return state.FirstNode;
        }

        private async Task appendToLastNode(
            node newNode, Tuple<node, int> lastNodeInfo,
            IAccessContext<CSScope> context)
        {
            await context.InChildWithin(async childContext =>
            {
                if (lastNodeInfo == null)
                {
                    /* Setting first node */
                    /* Requires an IInfiniteIntervalScope from 0 to infinity,
                     * because setting the first node determines
                     * could also change all other nodes. */

                    var scope = new CSScope(
                        RWScope.ReadWrite,
                        InfiniteIntervalScope.Create(
                            RWScope.ReadWrite, 0, 1),
                        $"scope for appendToLastNode {newNode.Value}");

                    /* TRAP: Using context instead of childContext here
                     * may easily lead to a deadlock. */
                    childContext.RequiredScope = scope;

                    /* Wait for all required scope before writing. */
                    await childContext.UntilAvailable();

                    state.FirstNode = newNode;
                }
                else
                {
                    /* Set the new node */

                    int newIndex = lastNodeInfo.Item2 + 1;

                    var scope = new CSScope(
                        RWScope.ReadWrite,
                        InfiniteIntervalScope.Create(
                            RWScope.ReadWrite, newIndex, newIndex + 1));
                    childContext.RequiredScope = scope;
                    await childContext.UntilAvailable();

                    lastNodeInfo.Item1.Successor = newNode;
                }
                state.Count++;
            });
        }

        private async Task add(T value, IAccessContext<CSScope> context)
        {
            await TaskCollector.With(async tc =>
            {
                CSScope pessimisticScope =
                    new CSScope(RWScope.ReadWrite,
                        InfiniteIntervalScope.Create(
                            RWScope.ReadWrite, 0, null),
                        $"pessimistic scope for add-{value}");

                await context.InChildWithin(pessimisticScope,
                    async childContext =>
                    {
                        Tuple<node, int> lastNodeInfo =
                            await getLastNode(childContext);

                        /* At this point, we don’t require write access
                     * to the nodes from 0 to count - 1 anymore,
                     * but IInfiniteIntervalScope is not granular enough
                     * to represent that.
                     * Read tasks for lower indexes could proceed. */

                        node newNode = new node
                        {
                            Successor = null,
                            Value = value
                        };

                        tc.Add(appendToLastNode(
                            newNode, lastNodeInfo, childContext));
                    });
            });
        }

        private ContextualStream(ContextualStream<T> parentContextual,
            IAccessContext<CSScope> context)
            : base(context, factory)
        {
            state = parentContextual.state;
        }

        private static readonly Func<ContextualStream<T>,
            IAccessContext<CSScope>, ContextualStream<T>>
            factory = (parentStream, context) =>
                new ContextualStream<T>(parentStream, context);

        private class streamData
        {
            public long Count = 0;
            /* Scope of this reference: [0; 0] */
            public node FirstNode = null;
        }

        private class node
        {
            public T Value;
            public node Successor;
        }

        private class enumerator : IAsyncEnumerator<T>
        {
            public T Current => GetCurrentAsync().Result.Value;

            public IReactive<T> GetCurrentAsync()
            {
                return Reactive.From<T>(async resultResolver =>
                {
                    using (ContextualStream<T> childStream =
                        stream.CreateChildWithin(new CSScope(valueScope:
                            InfiniteIntervalScope.Create(
                                RWScope.Read, 0, null))))
                    using (IAccessContext<enumeratorScope> childEnumContext =
                        context.CreateChildWithin(new enumeratorScope(
                            RWScope.Read,
                            RWScope.Read)))
                    {
                        long curNextIndex =
                            await readNextIndex(childEnumContext);
                        childEnumContext.RequiredScope =
                            new enumeratorScope(RWScope.Read);
                        CSScope readValueScope = new CSScope(valueScope:
                            InfiniteIntervalScope.Create(
                                RWScope.Read, curNextIndex - 1, curNextIndex));
                        childStream.Context.RequiredScope = readValueScope;

                        node curNode =
                            await readCurrentNode(childEnumContext);
                        childEnumContext.Dispose();

                        if (curNode == null)
                            throw new InvalidOperationException(
                                "The enumerator is already disposed.");

                        await childStream.Context.UntilAvailable(
                            readValueScope);
                        resultResolver.Resolve(curNode.Value);
                    }
                });
            }

            public bool MoveNext() => MoveNextAsync().Result.Value;

            public IReactive<bool> MoveNextAsync()
            {
                return Reactive.From<bool>(async resultResolver =>
                {
                    await TaskCollector.With(async tc =>
                    {
                        using (ContextualStream<T> childStream =
                            stream.CreateChildWithin(new CSScope(valueScope:
                                InfiniteIntervalScope.Create(
                                    RWScope.Read, 0, null))))
                        using (IAccessContext<enumeratorScope> childEnumContext
                            = context.CreateChild(new enumeratorScope(
                                RWScope.ReadWrite, RWScope.ReadWrite)))
                        {
                            long curNextIndex =
                                await readNextIndex(childEnumContext);
                            CSScope readNextNodeScope = new CSScope(valueScope:
                                InfiniteIntervalScope.Create(RWScope.Read,
                                    curNextIndex, curNextIndex + 1));
                            childStream.Context.RequiredScope =
                                readNextNodeScope;

                            node nextNode;

                            if (curNextIndex == 0)
                            {
                                /* Erste Iteration. */
                                tc.Add(incrementNextIndex(childEnumContext));
                                nextNode = await childStream.getFirstNode(
                                    childStream.Context);
                            }
                            else
                            {
                                node curCurrentNode =
                                    await readCurrentNode(childEnumContext);

                                if (curCurrentNode == null)
                                {
                                    throw new InvalidOperationException(
                                        "The stream has already " +
                                            "been iterated through.");
                                }
                                tc.Add(incrementNextIndex(childEnumContext));
                                await childStream.Context.UntilAvailable();
                                nextNode = curCurrentNode.Successor;
                            }
                            resultResolver.Resolve(nextNode != null);
                            childStream.Dispose();
                            tc.Add(setCurrentNode(nextNode, childEnumContext));
                        }
                    });
                });
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
                stream.Dispose();
                context.Dispose();
            }

            public enumerator(ContextualStream<T> stream)
            {
                this.stream = stream;

                //.CreateChild(new CSScope.Prototype
                //{
                //    countScope = RWScope.None,
                //    valueScope = new InfiniteIntervalScope(0, null, RWScope.Read)
                //});
            }

            #region private
            private readonly ContextualStream<T> stream;

            private readonly IAccessContext<enumeratorScope> context =
                AccessContext.Create(
                    enumeratorScopeLogic.Default,
                    enumeratorScopeLogic.MaximumScope);

            private node currentNode = null;
            private long nextIndex = 0;
            object IEnumerator.Current => Current;

            private async Task<node> readCurrentNode(
                IAccessContext<enumeratorScope> subContext)
            {
                await subContext.UntilAvailable(
                    new enumeratorScope(RWScope.Read));
                return currentNode;
            }

            private async Task setCurrentNode(
                node value, IAccessContext<enumeratorScope> subContext)
            {
                await subContext.UntilAvailable(
                    new enumeratorScope(RWScope.ReadWrite));
                currentNode = value;
            }

            private async Task<long> readNextIndex(
                IAccessContext<enumeratorScope> subContext)
            {
                await subContext.UntilAvailable(
                    new enumeratorScope(nextIndex: RWScope.Read));
                return nextIndex;
            }

            private async Task incrementNextIndex(
                IAccessContext<enumeratorScope> subContext)
            {
                /* These two statemets (InChild and UntilAvailable)
                 * could possibly be combined to a single
                 * (extension) method for AccessContext. */
                await subContext.InChild(new enumeratorScope(
                    nextIndex: RWScope.ReadWrite), async c =>
                    {
                        await subContext.UntilAvailable();
                        nextIndex++;
                    });
            }
            #endregion
        }

        private class enumeratorScope
        {
            public RWScope CurrentNode { get; }
            public RWScope NextIndex { get; }

            public ITupleScope<RWScope, RWScope> ToTupleScope() =>
                    TupleScope.Create(CurrentNode, NextIndex);

            public enumeratorScope(
                RWScope currentNode = RWScope.None,
                RWScope nextIndex = RWScope.None)
            {
                CurrentNode = currentNode;
                NextIndex = nextIndex;
            }

            public static enumeratorScope From(
                ITupleScope<RWScope, RWScope> scope) =>
                    new enumeratorScope(scope.Part0, scope.Part1);
        }

        private class enumeratorScopeLogic :
            IAccessScopeLogic<enumeratorScope>
        {
            public bool Contains(enumeratorScope outer, enumeratorScope inner)
                => tsi.Contains(outer.ToTupleScope(), inner.ToTupleScope());

            public bool Influences(
                enumeratorScope first, enumeratorScope second) =>
                    tsi.Influences(
                        first.ToTupleScope(), second.ToTupleScope());

            public enumeratorScope Intersect(
                enumeratorScope first, enumeratorScope second) =>
                    enumeratorScope.From(tsi.Intersect(
                        first.ToTupleScope(), second.ToTupleScope()));

            // ReSharper disable once StaticMemberInGenericType
            public static enumeratorScopeLogic Default { get; } =
                new enumeratorScopeLogic();

            // ReSharper disable once StaticMemberInGenericType
            public static enumeratorScope MaximumScope { get; } =
                new enumeratorScope(RWScope.ReadWrite, RWScope.ReadWrite);

            #region private

            // ReSharper disable once StaticMemberInGenericType
            private static readonly ITupleScopeLogic<RWScope, RWScope>
                tsi = TupleScopeLogic.Create(
                    RWScopeLogic.Instance, RWScopeLogic.Instance);
            #endregion
        }

        private class appender : IAppender<T>
        {
            public async Task Append(T value)
            {
                await TaskCollector.With(async tc =>
                {
                    using (var lknChild =
                        lastKnownNode.CreateChild(RWScope.ReadWrite))
                    using (var lknIndexChild =
                        lastKnownNodeIndex.CreateChild(RWScope.ReadWrite))
                    using (var streamChildContext =
                        stream.Context.CreateChildWithin(
                            new CSScope(
                                RWScope.ReadWrite,
                                InfiniteIntervalScope.Create(
                                    RWScope.ReadWrite, 0, null))))
                    {
                        node lkn = await tc.Adding(lknChild.Get());
                        int lknIndex;
                        if (lkn == null)
                            lknIndex = -1;
                        else
                            lknIndex = await tc.Adding(lknIndexChild.Get());
                        Tuple<node, int> lastNodeInfo =
                            await stream.getLastNode(
                                streamChildContext, lkn, lknIndex);
                        node newNode = new node
                        {
                            Value = value,
                            Successor = null,
                        };
                        tc.Add(stream.appendToLastNode(
                            newNode, lastNodeInfo, streamChildContext));
                        streamChildContext.Dispose();
                        tc.Add(lknChild.Set(newNode.ToFuture()));
                        tc.Add(lknIndexChild.Set(
                            ((lastNodeInfo?.Item2 ?? -1) + 1).ToFuture()));
                    }
                });
            }

            public appender(ContextualStream<T> stream)
            {
                this.stream = stream;
                lastKnownNode = AsyncBox.Create<node>(null);
                lastKnownNodeIndex = AsyncBox.Create(0);
            }

            #region private
            private readonly IAsyncBox<node> lastKnownNode;
            private readonly IAsyncBox<int> lastKnownNodeIndex;
            private readonly ContextualStream<T> stream;
            #endregion
        }
        #endregion
    }
}
