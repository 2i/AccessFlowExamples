#region copyright
// Copyright 2016 Christoph M�ller
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
    internal class FlagBasedAccessScopeLogic : IAccessScopeLogic<long>
    {
        public bool Influences(long first, long second)
        {
            return (first & second) != 0;
        }

        public bool Contains(long outer, long inner)
        {
            return (inner & outer) == inner;
        }

        public long Intersect(long outer, long inner)
        {
            return outer & inner;
        }

        public const long FullScope = ~0;
    }
}