using System;
using BitFaster.Caching.Lru;
using ObjectLayoutInspector;
using Xunit;
using Xunit.Abstractions;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class LruItemMemoryLayoutDumps
    {
        private readonly ITestOutputHelper testOutputHelper;

        public LruItemMemoryLayoutDumps(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        //Type layout for 'LruItem`2'
        //Size: 24 bytes. Paddings: 2 bytes (%8 of empty space)
        //|=====================================|
        //| Object Header (8 bytes)             |
        //|-------------------------------------|
        //| Method Table Ptr (8 bytes)          |
        //|=====================================|
        //|   0-7: Object data (8 bytes)        |
        //|-------------------------------------|
        //|  8-15: Object Key (8 bytes)         |
        //|-------------------------------------|
        //| 16-19: Int32 sequence (4 bytes)     |
        //|-------------------------------------|
        //|    20: Boolean wasAccessed (1 byte) |
        //|-------------------------------------|
        //|    21: Boolean wasRemoved (1 byte)  |
        //|-------------------------------------|
        //| 22-23: padding (2 bytes)            |
        //|=====================================|
        [Fact]
        public void DumpLruItem()
        {
            var layout = TypeLayout.GetLayout<LruItem<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }

        //Type layout for 'LongTickCountLruItem`2'
        //Size: 32 bytes. Paddings: 2 bytes (%6 of empty space)
        //|===================================================|
        //| Object Header (8 bytes)                           |
        //|---------------------------------------------------|
        //| Method Table Ptr (8 bytes)                        |
        //|===================================================|
        //|   0-7: Object data (8 bytes)                      |
        //|---------------------------------------------------|
        //|  8-15: Object Key (8 bytes)                       |
        //|---------------------------------------------------|
        //| 16-19: Int32 sequence (4 bytes)                   |
        //|---------------------------------------------------|
        //|    20: Boolean wasAccessed (1 byte)               |
        //|---------------------------------------------------|
        //|    21: Boolean wasRemoved (1 byte)                |
        //|---------------------------------------------------|
        //| 22-23: padding (2 bytes)                          |
        //|---------------------------------------------------|
        //| 24-31: Int64 <TickCount>k__BackingField (8 bytes) |
        //|===================================================|
        [Fact]
        public void DumpLongTickCountLruItem()
        {
            var layout = TypeLayout.GetLayout<LongTickCountLruItem<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }
    }
}
