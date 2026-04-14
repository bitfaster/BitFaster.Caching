using BitFaster.Caching.Lfu;
using ObjectLayoutInspector;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class NodeMemoryLayoutDumps
    {
        private readonly ITestOutputHelper testOutputHelper;

        public NodeMemoryLayoutDumps(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        //Type layout for 'AccessOrderNode`2'
        //Size: 48 bytes. Paddings: 0 bytes (%0 of empty space)
        //|=====================================================|
        //| Object Header (8 bytes)                             |
        //|-----------------------------------------------------|
        //| Method Table Ptr (8 bytes)                          |
        //|=====================================================|
        //|   0-7: Object data (8 bytes)                        |
        //|-----------------------------------------------------|
        //|  8-15: LfuNodeList`2 list (8 bytes)                 |
        //|-----------------------------------------------------|
        //| 16-23: LfuNode`2 next (8 bytes)                     |
        //|-----------------------------------------------------|
        //| 24-31: LfuNode`2 prev (8 bytes)                     |
        //|-----------------------------------------------------|
        //| 32-39: Object Key (8 bytes)                         |
        //|-----------------------------------------------------|
        //| 40-43: Int32 sequence (4 bytes)                     |
        //|-----------------------------------------------------|
        //| 44-45: Position <Position>k__BackingField (2 bytes) |
        //| |================================|                  |
        //| |   0-1: Int16 value__ (2 bytes) |                  |
        //| |================================|                  |
        //|-----------------------------------------------------|
        //|    46: Boolean wasRemoved (1 byte)                  |
        //|-----------------------------------------------------|
        //|    47: Boolean wasDeleted (1 byte)                  |
        //|=====================================================|
        [Fact]
        public void DumpAccessOrderNode()
        {
            var layout = TypeLayout.GetLayout<AccessOrderNode<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }

        // Type layout for 'TimeOrderNode`2'
        //Size: 72 bytes. Paddings: 0 bytes (%0 of empty space)
        //|=====================================================|
        //| Object Header (8 bytes)                             |
        //|-----------------------------------------------------|
        //| Method Table Ptr (8 bytes)                          |
        //|=====================================================|
        //|   0-7: Object data (8 bytes)                        |
        //|-----------------------------------------------------|
        //|  8-15: LfuNodeList`2 list (8 bytes)                 |
        //|-----------------------------------------------------|
        //| 16-23: LfuNode`2 next (8 bytes)                     |
        //|-----------------------------------------------------|
        //| 24-31: LfuNode`2 prev (8 bytes)                     |
        //|-----------------------------------------------------|
        //| 32-39: Object Key (8 bytes)                         |
        //|-----------------------------------------------------|
        //| 40-43: Int32 sequence (4 bytes)                     |
        //|-----------------------------------------------------|
        //| 44-45: Position <Position>k__BackingField (2 bytes) |
        //| |================================|                  |
        //| |   0-1: Int16 value__ (2 bytes) |                  |
        //| |================================|                  |
        //|-----------------------------------------------------|
        //|    46: Boolean wasRemoved (1 byte)                  |
        //|-----------------------------------------------------|
        //|    47: Boolean wasDeleted (1 byte)                  |
        //|-----------------------------------------------------|
        //| 48-55: TimeOrderNode`2 prevTime (8 bytes)           |
        //|-----------------------------------------------------|
        //| 56-63: TimeOrderNode`2 nextTime (8 bytes)           |
        //|-----------------------------------------------------|
        //| 64-71: Duration timeToExpire (8 bytes)              |
        //| |============================|                      |
        //| |   0-7: Int64 raw (8 bytes) |                      |
        //| |============================|                      |
        //|=====================================================|
        [Fact]
        public void DumpTimeOrderNode()
        {
            var layout = TypeLayout.GetLayout<TimeOrderNode<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }
    }
}
