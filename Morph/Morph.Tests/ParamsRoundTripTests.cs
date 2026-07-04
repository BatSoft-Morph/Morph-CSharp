using Morph.Base;
using Morph.Core;
using Morph.Params;
using NUnit.Framework;
using System;

namespace Morph.Tests
{
    /// <summary>
    /// Round-trips values through Parameters.Encode / Parameters.Decode,
    /// exercising the wire format defined in "Morph Protocol.xlsx".
    /// </summary>
    [TestFixture]
    public class ParamsRoundTripTests
    {
        #region Helpers

        private static object RoundTrip(object value)
            => RoundTrip(value, new InstanceFactories());

        private static object RoundTrip(object value, InstanceFactories factories)
        {
            MorphWriter writer = Parameters.Encode(new object[] { value }, factories);
            MorphReader reader = new LinkData(writer).Reader;
            Parameters.Decode(factories, null, reader, out object[] decoded);
            Assert.That(decoded, Is.Not.Null);
            Assert.That(decoded.Length, Is.EqualTo(1));
            return decoded[0];
        }

        #endregion

        #region Simple types

        [TestCase(false)]
        [TestCase(true)]
        public void Boolean_RoundTrips(bool value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(byte.MinValue)]
        [TestCase(byte.MaxValue)]
        public void Byte_RoundTrips(byte value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(ushort.MinValue)]
        [TestCase(ushort.MaxValue)]
        public void UInt16_RoundTrips(ushort value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(uint.MinValue)]
        [TestCase(uint.MaxValue)]
        public void UInt32_RoundTrips(uint value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(ulong.MinValue)]
        [TestCase(ulong.MaxValue)]
        public void UInt64_RoundTrips(ulong value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(sbyte.MinValue)]
        [TestCase(sbyte.MaxValue)]
        public void SByte_RoundTrips(sbyte value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(short.MinValue)]
        [TestCase(short.MaxValue)]
        public void Int16_RoundTrips(short value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(int.MinValue)]
        [TestCase(0)]
        [TestCase(int.MaxValue)]
        public void Int32_RoundTrips(int value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        public void Int64_RoundTrips(long value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(0f)]
        [TestCase(-1.5f)]
        [TestCase(float.MaxValue)]
        [TestCase(float.Epsilon)]
        public void Single_RoundTrips(float value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase(0d)]
        [TestCase(Math.PI)]
        [TestCase(double.MaxValue)]
        [TestCase(double.Epsilon)]
        public void Double_RoundTrips(double value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase('A')]
        [TestCase('Ω')]
        public void Char_RoundTrips(char value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [TestCase("")]
        [TestCase("Hello")]
        [TestCase("Unicode: åäö ✓ 你好")]
        public void String_RoundTrips(string value)
            => Assert.That(RoundTrip(value), Is.EqualTo(value));

        [Test]
        public void Null_RoundTrips()
            => Assert.That(RoundTrip(null), Is.Null);

        #endregion

        #region Currency

        [TestCase("0")]
        [TestCase("123.4567")]
        [TestCase("-99999.9999")]
        public void Currency_RoundTrips(string text)
        {
            //  Currency is fixed point scaled by 10 000, so 4 decimals survive
            decimal value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(RoundTrip(value), Is.EqualTo(value));
        }

        #endregion

        #region Date/time

        [Test]
        public void DateTimeUtc_RoundTrips()
        {
            //  The wire format has second precision, and 'Z' marks UTC
            DateTime value = new DateTime(2026, 7, 4, 12, 34, 56, DateTimeKind.Utc);
            DateTime result = (DateTime)RoundTrip(value);
            Assert.That(result, Is.EqualTo(value));
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void DateTimeLocal_RoundTrips()
        {
            //  Local times travel without 'Z':  "the local timezone, whatever that may currently be"
            DateTime value = new DateTime(2026, 7, 4, 12, 34, 56, DateTimeKind.Local);
            DateTime result = (DateTime)RoundTrip(value);
            Assert.That(result, Is.EqualTo(value));
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Local));
        }

        [Test]
        public void TimeSpan_RoundTrips()
        {
            TimeSpan value = new TimeSpan(1, 2, 3, 4);
            Assert.That(RoundTrip(value), Is.EqualTo(value));
        }

        #endregion

        #region Enums

        private enum TestColour : byte
        {
            Red = 1,
            Green = 2,
            Blue = 3,
        }

        [Flags]
        private enum TestOptions
        {
            None = 0,
            First = 1,
            Second = 2,
        }

        [Test]
        public void Enum_RegisteredType_RoundTripsTyped()
        {
            InstanceFactories factories = new InstanceFactories();
            factories.AddEnumType(typeof(TestColour));
            Assert.That(RoundTrip(TestColour.Green, factories), Is.EqualTo(TestColour.Green));
        }

        [Test]
        public void Enum_UnregisteredType_DegradesToOrdinal()
        {
            //  "TestColour : byte" travels as a 1 byte ordinal
            Assert.That(RoundTrip(TestColour.Blue), Is.EqualTo((byte)3));
        }

        [Test]
        public void EnumArray_RegisteredType_RoundTripsTyped()
        {
            InstanceFactories factories = new InstanceFactories();
            factories.AddEnumType(typeof(TestColour));
            TestColour[] value = new TestColour[] { TestColour.Red, TestColour.Blue };
            object result = RoundTrip(value, factories);
            Assert.That(result, Is.InstanceOf<TestColour[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void FlagsEnum_RegisteredType_RoundTripsSet()
        {
            InstanceFactories factories = new InstanceFactories();
            factories.AddEnumType(typeof(TestOptions));
            TestOptions value = TestOptions.First | TestOptions.Second;
            Assert.That(RoundTrip(value, factories), Is.EqualTo(value));
        }

        #endregion

        #region Arrays

        [Test]
        public void ByteArray_RoundTrips()
        {
            byte[] value = new byte[] { 0, 1, 127, 255 };
            Assert.That(RoundTrip(value), Is.EqualTo(value));
        }

        [Test]
        public void IntArray_WithRegisteredFactory_RoundTripsTyped()
        {
            InstanceFactoryArray arrayFactory = new InstanceFactoryArray();
            arrayFactory.AddArrayElemType(typeof(int));
            InstanceFactories factories = new InstanceFactories();
            factories.Add(arrayFactory);
            int[] value = new int[] { int.MinValue, 0, 42, int.MaxValue };
            object result = RoundTrip(value, factories);
            Assert.That(result, Is.InstanceOf<int[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void IntArray_WithoutFactory_RoundTripsTyped()
        {
            //  The wire declares the element type, so the typed array comes back without any factory
            int[] value = new int[] { 1, 2, 3 };
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<int[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void CharArray_RoundTripsTyped()
        {
            //  Methods taking char[] must receive char[], not object[]  (Endpoint.LinkMethod invocation)
            char[] value = new char[] { 'M', 'o', 'r', 'p', 'h' };
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<char[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void BoolArray_RoundTripsTyped()
        {
            bool[] value = new bool[] { true, false, true };
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<bool[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void DoubleArray_RoundTripsTyped()
        {
            double[] value = new double[] { 1.5, -2.5, Math.PI };
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<double[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void StringArray_WithNulls_RoundTripsTyped()
        {
            //  Strings may be null, so string elements each carry their own ValueType;
            //  the declared "String[]" is rebuilt from the wire TypeName
            string[] value = new string[] { "first", null, "third" };
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<string[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void MixedArray_RoundTrips()
        {
            //  A genuine object[] of mixed types stays an object[]
            object[] value = new object[] { 1, "two", null, true };
            object result = RoundTrip(value);
            Assert.That(result.GetType(), Is.EqualTo(typeof(object[])));
            Assert.That(result, Is.EqualTo(value));
        }

        [Test]
        public void EmptyArray_RoundTripsTyped()
        {
            int[] value = new int[0];
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<int[]>());
            Assert.That(result, Is.EqualTo(value));
        }

        #region The reported failure:  invoking a method that expects char[]

        public class EchoService
        {
            public string Text(char[] chars)
                => new string(chars);
        }

        [Test]
        public void DecodedCharArray_BindsToMethodExpectingCharArray()
        {
            //  Mirrors Endpoint.LinkMethod:  decode the parameters, then MethodInfo.Invoke
            InstanceFactories factories = new InstanceFactories();
            MorphWriter writer = Parameters.Encode(new object[] { new char[] { 'H', 'i' } }, factories);
            MorphReader reader = new LinkData(writer).Reader;
            Parameters.Decode(factories, null, reader, out object[] paramsIn);
            EchoService service = new EchoService();
            object result = typeof(EchoService).GetMethod(nameof(EchoService.Text)).Invoke(service, paramsIn);
            Assert.That(result, Is.EqualTo("Hi"));
        }

        #endregion

        #endregion

        #region Structs

        public struct TestStruct
        {
            public int Number;
            public string Text;
        }

        [Test]
        public void Struct_WithRegisteredFactory_RoundTripsTyped()
        {
            InstanceFactoryStruct structFactory = new InstanceFactoryStruct();
            structFactory.AddStructType(typeof(TestStruct));
            InstanceFactories factories = new InstanceFactories();
            factories.Add(structFactory);
            TestStruct value = new TestStruct { Number = 42, Text = "hello" };
            object result = RoundTrip(value, factories);
            Assert.That(result, Is.InstanceOf<TestStruct>());
            TestStruct typed = (TestStruct)result;
            Assert.That(typed.Number, Is.EqualTo(42));
            Assert.That(typed.Text, Is.EqualTo("hello"));
        }

        [Test]
        public void Struct_WithoutFactory_DegradesToValueInstance()
        {
            TestStruct value = new TestStruct { Number = 7, Text = "seven" };
            object result = RoundTrip(value);
            Assert.That(result, Is.InstanceOf<ValueInstance>());
            ValueInstance instance = (ValueInstance)result;
            Assert.That(instance.TypeName, Is.EqualTo("TestStruct"));
            Assert.That(instance.Struct.ByName("Number"), Is.EqualTo(7));
            Assert.That(instance.Struct.ByName("Text"), Is.EqualTo("seven"));
        }

        #endregion

        #region Parameter lists

        [Test]
        public void NoParams_OmitsDataEntirely()
        {
            Assert.That(Parameters.Encode(null, new InstanceFactories()), Is.Null);
            Assert.That(Parameters.Encode(new object[0], new InstanceFactories()), Is.Null);
        }

        [Test]
        public void ParamsAndSpecial_SpecialIsLastAndIncludedInCount()
        {
            InstanceFactories factories = new InstanceFactories();
            MorphWriter writer = Parameters.Encode(new object[] { 1, 2 }, "result", factories);
            MorphReader reader = new LinkData(writer).Reader;
            Parameters.Decode(factories, null, reader, out object[] Params, out object special);
            Assert.That(Params, Is.EqualTo(new object[] { 1, 2 }));
            Assert.That(special, Is.EqualTo("result"));
        }

        [Test]
        public void NullSpecialAlone_RoundTrips()
        {
            InstanceFactories factories = new InstanceFactories();
            MorphWriter writer = Parameters.Encode(null, null, factories);
            Assert.That(writer, Is.Not.Null);   //  A null Special is still a parameter
            MorphReader reader = new LinkData(writer).Reader;
            Parameters.Decode(factories, null, reader, out object[] Params, out object special);
            Assert.That(Params, Is.Null);
            Assert.That(special, Is.Null);
        }

        #endregion
    }
}
