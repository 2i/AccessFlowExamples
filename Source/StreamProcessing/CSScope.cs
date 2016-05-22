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
using AccessFlow.BasicScopes;

namespace StreamProcessing
{
    public class CSScope
    {
        public RWScope CountScope { get; }
        public IInfiniteIntervalScope<RWScope> ValueScope { get; }

        /// <remarks>
        /// Only for debugging purposes.
        /// </remarks>
        public string Name { get; set; }

        public override string ToString()
        {
            return $"CountScope: ({CountScope}); " +
                $"ValueScope: ({ValueScope}); " +
                $"Name: ({Name})";
        }

        public ITupleScope<RWScope, IInfiniteIntervalScope<RWScope>> 
            ToTupleScope()
        {
            return TupleScope.Create(CountScope, ValueScope);
        }

        public CSScope(
            RWScope countScope = RWScope.None,
            IInfiniteIntervalScope<RWScope> valueScope = null,
            string name = null)
        {
            if (valueScope == null)
                valueScope = InfiniteIntervalScope.Create(RWScope.None);
            CountScope = countScope;
            ValueScope = valueScope;
            Name = name;
        }

        public static CSScope From(
            ITupleScope<RWScope, IInfiniteIntervalScope<RWScope>> scope)
        {
            return new CSScope(scope.Part0, scope.Part1);
        }
    }
}
