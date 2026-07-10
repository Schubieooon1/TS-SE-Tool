namespace TS_SE_Tool.Save.Items
{
    internal class SiiNBlockCore
    {
        /// <summary>
        /// Legacy serializers used this callback to remove blocks from a global
        /// traversal list. SiiNunit now owns block ordering and writes every block
        /// exactly once, so serializers must no longer mutate global state.
        /// </summary>
        internal void removeWritenBlock(string input)
        {
            // Intentionally left blank for backwards compatibility with all existing
            // block classes that still call this method from PrintOut().
        }
    }
}
