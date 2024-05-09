﻿using System;
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
        //Size: 24 bytes.Paddings: 6 bytes(%25 of empty space)
        //|===============================================|
        //| Object Header(8 bytes)                        |
        //|-----------------------------------------------|
        //| Method Table Ptr(8 bytes)                     |
        //|===============================================|
        //|   0-7: Object Key(8 bytes)                    |
        //|-----------------------------------------------|
        //|  8-15: Object<Value> k__BackingField(8 bytes) |
        //|-----------------------------------------------|
        //|    16: Boolean wasAccessed(1 byte)            |
        //|-----------------------------------------------|
        //|    17: Boolean wasRemoved(1 byte)             |
        //|-----------------------------------------------|
        //| 18-23: padding(6 bytes)                       |
        //|===============================================|
        [Fact]
        public void DumpLruItem()
        { 
            var layout = TypeLayout.GetLayout<LruItem<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }

        //Type layout for 'LongTickCountLruItem`2'
        //Size: 32 bytes.Paddings: 6 bytes(%18 of empty space)
        //|==================================================|
        //| Object Header(8 bytes)                           |
        //|--------------------------------------------------|
        //| Method Table Ptr(8 bytes)                        |
        //|==================================================|
        //|   0-7: Object Key(8 bytes)                       |
        //|--------------------------------------------------|
        //|  8-15: Object<Value> k__BackingField(8 bytes)    |
        //|--------------------------------------------------|
        //|    16: Boolean wasAccessed(1 byte)               |
        //|--------------------------------------------------|
        //|    17: Boolean wasRemoved(1 byte)                |
        //|--------------------------------------------------|
        //| 18-23: padding(6 bytes)                          |
        //|--------------------------------------------------|
        //| 24-31: Int64<TickCount> k__BackingField(8 bytes) |
        //|==================================================|
        [Fact]
        public void DumpLongTickCountLruItem()
        {
            var layout = TypeLayout.GetLayout<LongTickCountLruItem<object, object>>(includePaddings: true);
            testOutputHelper.WriteLine(layout.ToString());
        }
    }
}
