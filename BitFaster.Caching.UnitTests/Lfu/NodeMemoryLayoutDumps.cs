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
        //Size: 48 bytes.Paddings: 2 bytes(%4 of empty space)
        //|====================================================|
        //| Object Header(8 bytes)                             |
        //|----------------------------------------------------|
        //| Method Table Ptr(8 bytes)                          |
        //|====================================================|
        //|   0-7: LfuNodeList`2 list(8 bytes)                 |
        //|----------------------------------------------------|
        //|  8-15: LfuNode`2 next(8 bytes)                     |
        //|----------------------------------------------------|
        //| 16-23: LfuNode`2 prev(8 bytes)                     |
        //|----------------------------------------------------|
        //| 24-31: Object Key(8 bytes)                         |
        //|----------------------------------------------------|
        //| 32-39: Object<Value> k__BackingField(8 bytes)      |
        //|----------------------------------------------------|
        //| 40-43: Position<Position> k__BackingField(4 bytes) |
        //| |===============================|                  |
        //| |   0-3: Int32 value__(4 bytes) |                  |
        //| |===============================|                  |
        //|----------------------------------------------------|
        //|    44: Boolean wasRemoved(1 byte)                  |
        //|----------------------------------------------------|
        //|    45: Boolean wasDeleted(1 byte)                  |
        //|----------------------------------------------------|
        //| 46-47: padding(2 bytes)                            |
        //|====================================================|
        [Fact]
        public void DumpAccessOrderNode()
        { 
            var layout = TypeLayout.GetLayout<AccessOrderNode<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }

        //Type layout for 'TimeOrderNode`2'
        //Size: 72 bytes.Paddings: 2 bytes(%2 of empty space)
        //|====================================================|
        //| Object Header(8 bytes)                             |
        //|----------------------------------------------------|
        //| Method Table Ptr(8 bytes)                          |
        //|====================================================|
        //|   0-7: LfuNodeList`2 list(8 bytes)                 |
        //|----------------------------------------------------|
        //|  8-15: LfuNode`2 next(8 bytes)                     |
        //|----------------------------------------------------|
        //| 16-23: LfuNode`2 prev(8 bytes)                     |
        //|----------------------------------------------------|
        //| 24-31: Object Key(8 bytes)                         |
        //|----------------------------------------------------|
        //| 32-39: Object<Value> k__BackingField(8 bytes)      |
        //|----------------------------------------------------|
        //| 40-43: Position<Position> k__BackingField(4 bytes) |
        //| |===============================|                  |
        //| |   0-3: Int32 value__(4 bytes) |                  |
        //| |===============================|                  |
        //|----------------------------------------------------|
        //|    44: Boolean wasRemoved(1 byte)                  |
        //|----------------------------------------------------|
        //|    45: Boolean wasDeleted(1 byte)                  |
        //|----------------------------------------------------|
        //| 46-47: padding(2 bytes)                            |
        //|----------------------------------------------------|
        //| 48-55: TimeOrderNode`2 prevTime(8 bytes)           |
        //|----------------------------------------------------|
        //| 56-63: TimeOrderNode`2 nextTime(8 bytes)           |
        //|----------------------------------------------------|
        //| 64-71: Duration timeToExpire(8 bytes)              |
        //| |===========================|                      |
        //| |   0-7: Int64 raw(8 bytes) |                      |
        //| |===========================|                      |
        //|====================================================|
        [Fact]
        public void DumpTimeOrderNode()
        {
            var layout = TypeLayout.GetLayout<TimeOrderNode<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }
    }
}
