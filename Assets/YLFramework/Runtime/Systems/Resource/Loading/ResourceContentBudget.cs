using System;

namespace YLFramework.Services.Resource
{
    // 预算随原 BundleRecord/input 归属；等待者和 native 并发槽不是驻留计量单位。
    internal sealed class ResourceContentBudget
    {
        private readonly object _gate = new();
        internal long MaximumBytes
        {
            get;
        }
        internal long AllocatedBytes
        {
            get; private set;
        }
        internal long ReservedBytes
        {
            get; private set;
        }
        internal long PeakBytes
        {
            get; private set;
        }
        internal int LiveInputs
        {
            get; private set;
        }

        internal ResourceContentBudget(long maximumBytes)
        {
            if (maximumBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            }

            MaximumBytes = maximumBytes;
        }

        internal (long Allocated, long Reserved, long Peak, int Inputs) Capture()
        {
            lock (_gate)
            {
                return (AllocatedBytes, ReservedBytes, PeakBytes, LiveInputs);
            }
        }

        internal ResourceContentReservation[] Reserve(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                return Array.Empty<ResourceContentReservation>();
            }

            lock (_gate)
            {
                var bytes = checked((long)count * ResourceDecryptStream.DataBufferBytes);
                var used = checked(AllocatedBytes + ReservedBytes);
                if (bytes > MaximumBytes - used)
                {
                    throw new ResourceException(ResourceError.ContentBufferUnavailable,
                        $"Content buffers require {bytes} additional bytes; allocated={AllocatedBytes}, reserved={ReservedBytes}, limit={MaximumBytes}. Release owned resources and explicitly retry.");
                }

                var tickets = new ResourceContentReservation[count];
                for (var i = 0; i < count; i++)
                {
                    tickets[i] = new ResourceContentReservation(this);
                }

                ReservedBytes += bytes;
                PeakBytes = Math.Max(PeakBytes, used + bytes);
                return tickets;
            }
        }

        internal void Allocate(ResourceContentReservation ticket)
        {
            lock (_gate)
            {
                if (ticket.State != 0)
                {
                    throw new InvalidOperationException("A content reservation can only be allocated once.");
                }

                ticket.State = 1;
                ReservedBytes -= ResourceDecryptStream.DataBufferBytes;
                AllocatedBytes += ResourceDecryptStream.DataBufferBytes;
                LiveInputs++;
            }
        }

        internal void Release(ResourceContentReservation ticket)
        {
            lock (_gate)
            {
                if (ticket.State == 2)
                {
                    return;
                }

                if (ticket.State == 0)
                {
                    ReservedBytes -= ResourceDecryptStream.DataBufferBytes;
                }
                else
                {
                    AllocatedBytes -= ResourceDecryptStream.DataBufferBytes;
                    LiveInputs--;
                }
                ticket.State = 2;
            }
        }

        internal void PrepareRetry(ResourceContentReservation ticket)
        {
            lock (_gate)
            {
                if (ticket.State == 2)
                {
                    throw new ObjectDisposedException(nameof(ResourceContentReservation));
                }

                if (ticket.State == 0)
                {
                    return;
                }

                AllocatedBytes -= ResourceDecryptStream.DataBufferBytes;
                ReservedBytes += ResourceDecryptStream.DataBufferBytes;
                LiveInputs--;
                ticket.State = 0;
            }
        }
    }

    internal sealed class ResourceContentReservation : IDisposable
    {
        private readonly ResourceContentBudget _budget;
        internal int State;
        internal ResourceContentReservation(ResourceContentBudget budget) => this._budget = budget;
        internal void Allocate() => _budget.Allocate(this);
        internal void PrepareRetry() => _budget.PrepareRetry(this);
        public void Dispose() => _budget.Release(this);
    }
}
