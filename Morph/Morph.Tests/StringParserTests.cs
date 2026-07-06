using Morph.Lib;
using NUnit.Framework;

namespace Morph.Tests
{
    /// <summary>
    /// StringParser's "try to read" methods are used by the best-effort IPv4 parser in
    /// MorphApartmentProxy.ResolveIPv4, which must return null (so Resolve falls back to DNS) for
    /// anything that is not a dotted-quad.  So ReadChars/ReadDigits/ReadChar must treat
    /// end-of-string as "nothing to read", NOT throw - otherwise ViaString throws a
    /// StringParserException for an empty, short, or trailing-dot address.
    /// </summary>
    [TestFixture]
    public class StringParserTests
    {
        [Test]
        public void ReadDigits_OnEmptyString_ReturnsNullNotThrow()
        {
            StringParser parser = new StringParser("");
            Assert.That(parser.ReadDigits(), Is.Null);
        }

        [Test]
        public void ReadChar_AtEndOfString_ReturnsFalseNotThrow()
        {
            StringParser parser = new StringParser("");
            Assert.That(parser.ReadChar('.'), Is.False);
        }

        [Test]
        public void TrailingDotAddress_ParsesToEnd_WithoutThrowing()
        {
            //  Mirrors the address case that crashed Connect: digits, dot, then end-of-string.
            StringParser parser = new StringParser("12.");
            Assert.That(parser.ReadDigits(), Is.EqualTo("12"));
            Assert.That(parser.ReadChar('.'), Is.True);
            Assert.That(parser.ReadDigits(), Is.Null);   //  at end - must return null, not throw
            Assert.That(parser.IsEnded(), Is.True);
        }

        [Test]
        public void ReadDigits_ReadsLeadingDigitsOnly()
        {
            StringParser parser = new StringParser("127x");
            Assert.That(parser.ReadDigits(), Is.EqualTo("127"));
            Assert.That(parser.ReadChar('.'), Is.False);   //  'x' is not '.', mid-string
        }
    }
}
