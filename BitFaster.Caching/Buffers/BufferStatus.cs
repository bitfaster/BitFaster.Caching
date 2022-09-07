
namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Specifies the status of buffer operations.
    /// </summary>
    public enum BufferStatus
    {
        /// <summary>
        /// The buffer is full.
        /// </summary>
        Full,

        /// <summary>
        /// The buffer is empty.
        /// </summary>
        Empty,

        /// <summary>
        /// The buffer operation succeeded.
        /// </summary>
        Success,

        /// <summary>
        /// The buffer operation was contended.
        /// </summary>
        Contended,
    }
}
