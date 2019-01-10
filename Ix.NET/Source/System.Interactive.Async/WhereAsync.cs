// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<TSource> WhereAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<bool>> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            // TODO: Can we add array/list optimizations here, does it make sense?
            return new WhereAsyncEnumerableAsyncIterator<TSource>(source, predicate);
        }

        public static IAsyncEnumerable<TSource> WhereAsync<TSource>(this IAsyncEnumerable<TSource> source, Func<TSource, int, Task<bool>> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return new WhereAsyncEnumerableWithIndexAsyncIterator<TSource>(source, predicate);
        }

        private static Func<TSource, Task<bool>> CombineAsyncPredicates<TSource>(Func<TSource, Task<bool>> predicate1, Func<TSource, Task<bool>> predicate2)
        {
            return async (x) => await predicate1(x) && await predicate2(x);
        }

        private static Func<TSource, Task<bool>> CombineAsyncPredicates<TSource>(Func<TSource, bool> predicate1, Func<TSource, Task<bool>> predicate2)
        {
            return async (x) => predicate1(x) && await predicate2(x);
        }

        private static Func<TSource, Task<bool>> CombineAsyncPredicates<TSource>(Func<TSource, Task<bool>> predicate1, Func<TSource, bool> predicate2)
        {
            return async (x) => await predicate1(x) && predicate2(x);
        }

        internal sealed class WhereAsyncEnumerableAsyncIterator<TSource> : AsyncIterator<TSource>
        {
            private readonly Func<TSource, Task<bool>> predicate;
            private readonly IAsyncEnumerable<TSource> source;
            private IAsyncEnumerator<TSource> enumerator;

            public WhereAsyncEnumerableAsyncIterator(IAsyncEnumerable<TSource> source, Func<TSource, Task<bool>> predicate)
            {
                Debug.Assert(source != null);
                Debug.Assert(predicate != null);

                this.source = source;
                this.predicate = predicate;
            }

            public override AsyncIterator<TSource> Clone()
            {
                return new WhereAsyncEnumerableAsyncIterator<TSource>(source, predicate);
            }

            public override void Dispose()
            {
                if (enumerator != null)
                {
                    enumerator.Dispose();
                    enumerator = null;
                }
                base.Dispose();
            }

            public override IAsyncEnumerable<TResult> Select<TResult>(Func<TSource, TResult> selector)
            {
                return new WhereAsyncSelectEnumerableAsyncIterator<TSource, TResult>(source, predicate, selector);
            }

            public override IAsyncEnumerable<TSource> Where(Func<TSource, bool> predicate)
            {
                return new WhereAsyncEnumerableAsyncIterator<TSource>(source, CombineAsyncPredicates(this.predicate, predicate));
            }

            protected override async Task<bool> MoveNextCore(CancellationToken cancellationToken)
            {
                switch (state)
                {
                    case AsyncIteratorState.Allocated:
                        enumerator = source.GetEnumerator();
                        state = AsyncIteratorState.Iterating;
                        goto case AsyncIteratorState.Iterating;

                    case AsyncIteratorState.Iterating:
                        while (await enumerator.MoveNext(cancellationToken)
                                               .ConfigureAwait(false))
                        {
                            var item = enumerator.Current;
                            if (await predicate(item).ConfigureAwait(false))
                            {
                                current = item;
                                return true;
                            }
                        }

                        Dispose();
                        break;
                }

                return false;
            }
        }

        internal sealed class WhereAsyncEnumerableWithIndexAsyncIterator<TSource> : AsyncIterator<TSource>
        {
            private readonly Func<TSource, int, Task<bool>> predicate;
            private readonly IAsyncEnumerable<TSource> source;

            private IAsyncEnumerator<TSource> enumerator;
            private int index;

            public WhereAsyncEnumerableWithIndexAsyncIterator(IAsyncEnumerable<TSource> source, Func<TSource, int, Task<bool>> predicate)
            {
                Debug.Assert(source != null);
                Debug.Assert(predicate != null);

                this.source = source;
                this.predicate = predicate;
            }

            public override AsyncIterator<TSource> Clone()
            {
                return new WhereAsyncEnumerableWithIndexAsyncIterator<TSource>(source, predicate);
            }

            public override void Dispose()
            {
                if (enumerator != null)
                {
                    enumerator.Dispose();
                    enumerator = null;
                }
                base.Dispose();
            }

            protected override async Task<bool> MoveNextCore(CancellationToken cancellationToken)
            {
                switch (state)
                {
                    case AsyncIteratorState.Allocated:
                        enumerator = source.GetEnumerator();
                        index = -1;
                        state = AsyncIteratorState.Iterating;
                        goto case AsyncIteratorState.Iterating;

                    case AsyncIteratorState.Iterating:
                        while (await enumerator.MoveNext(cancellationToken)
                                               .ConfigureAwait(false))
                        {
                            checked
                            {
                                index++;
                            }
                            var item = enumerator.Current;
                            if (await predicate(item, index).ConfigureAwait(false))
                            {
                                current = item;
                                return true;
                            }
                        }

                        Dispose();
                        break;
                }

                return false;
            }
        }

        internal sealed class WhereAsyncSelectEnumerableAsyncIterator<TSource, TResult> : AsyncIterator<TResult>
        {
            private readonly Func<TSource, Task<bool>> predicate;
            private readonly Func<TSource, TResult> selector;
            private readonly IAsyncEnumerable<TSource> source;

            private IAsyncEnumerator<TSource> enumerator;

            public WhereAsyncSelectEnumerableAsyncIterator(IAsyncEnumerable<TSource> source, Func<TSource, Task<bool>> predicate, Func<TSource, TResult> selector)
            {
                Debug.Assert(source != null);
                Debug.Assert(predicate != null);
                Debug.Assert(selector != null);

                this.source = source;
                this.predicate = predicate;
                this.selector = selector;
            }

            public override AsyncIterator<TResult> Clone()
            {
                return new WhereAsyncSelectEnumerableAsyncIterator<TSource, TResult>(source, predicate, selector);
            }

            public override void Dispose()
            {
                if (enumerator != null)
                {
                    enumerator.Dispose();
                    enumerator = null;
                }

                base.Dispose();
            }

            public override IAsyncEnumerable<TResult1> Select<TResult1>(Func<TResult, TResult1> selector)
            {
                return new WhereAsyncSelectEnumerableAsyncIterator<TSource, TResult1>(source, predicate, CombineSelectors(this.selector, selector));
            }

            protected override async Task<bool> MoveNextCore(CancellationToken cancellationToken)
            {
                switch (state)
                {
                    case AsyncIteratorState.Allocated:
                        enumerator = source.GetEnumerator();
                        state = AsyncIteratorState.Iterating;
                        goto case AsyncIteratorState.Iterating;

                    case AsyncIteratorState.Iterating:
                        while (await enumerator.MoveNext(cancellationToken)
                                               .ConfigureAwait(false))
                        {
                            var item = enumerator.Current;
                            if (await predicate(item).ConfigureAwait(false))
                            {
                                current = selector(item);
                                return true;
                            }
                        }

                        Dispose();
                        break;
                }

                return false;
            }
        }
    }
}
