﻿using CSnakes.Runtime.Python;
using System.ComponentModel;

namespace CSnakes.Runtime.Tests.Converter;

public class DictionaryConverterTest : RuntimeTestBase
{
    [Fact]
    public void DictionaryConverter()
    {
        Dictionary<string, string> input = new()
        {
            ["Hello"] = "World?",
            ["Foo"] = "Bar"
        };
        TypeConverter td = TypeDescriptor.GetConverter(typeof(PyObject));
        Assert.True(td.CanConvertFrom(input.GetType()));
        using (GIL.Acquire())
        {
            using PyObject? pyObj = td.ConvertFrom(input) as PyObject;
            Assert.NotNull(pyObj);
            Assert.True(td.CanConvertTo(input.GetType()));
            // Convert back
            object? str = td.ConvertTo(pyObj, input.GetType());
            Assert.Equal(input, str);
        }
    }
}
