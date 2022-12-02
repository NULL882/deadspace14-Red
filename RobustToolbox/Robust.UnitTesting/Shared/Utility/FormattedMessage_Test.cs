using System.Linq;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.UnitTesting.Shared.Utility
{
    [Parallelizable(ParallelScope.All)]
    [TestFixture]
    [TestOf(typeof(FormattedMessage))]
    public sealed class FormattedMessage_Test
    {
        [Test]
        public static void TestParseMarkup()
        {
            var msg = FormattedMessage.FromMarkup("foo[color=#aabbcc]bar[/color]baz");

            Assert.That(msg.Tags, NUnit.Framework.Is.EquivalentTo(new FormattedMessage.Tag[]
            {
                new FormattedMessage.TagText("foo"),
                new FormattedMessage.TagColor(Color.FromHex("#aabbcc")),
                new FormattedMessage.TagText("bar"),
                FormattedMessage.TagPop.Instance,
                new FormattedMessage.TagText("baz")
            }));
        }

        [Test]
        public static void TestParseMarkupColorName()
        {
            var msg = FormattedMessage.FromMarkup("foo[color=orange]bar[/color]baz");

            Assert.That(msg.Tags, NUnit.Framework.Is.EquivalentTo(new FormattedMessage.Tag[]
            {
                new FormattedMessage.TagText("foo"),
                new FormattedMessage.TagColor(Color.Orange),
                new FormattedMessage.TagText("bar"),
                FormattedMessage.TagPop.Instance,
                new FormattedMessage.TagText("baz")
            }));
        }

        [Test]
        [TestCase("foo[color=#aabbcc bar")]
        [TestCase("foo[color #aabbcc] bar")]
        [TestCase("foo[stinky] bar")]
        public static void TestParsePermissiveMarkup(string text)
        {
            var msg = FormattedMessage.FromMarkupPermissive(text);

            Assert.That(
                string.Join("", msg.Tags.Cast<FormattedMessage.TagText>().Select(p => p.Text)),
                NUnit.Framework.Is.EqualTo(text));
        }

        [Test]
        [TestCase("Foo", ExpectedResult = "Foo")]
        [TestCase("[color=red]Foo[/color]", ExpectedResult = "Foo")]
        [TestCase("[color=red]Foo[/color]bar", ExpectedResult = "Foobar")]
        public string TestRemoveMarkup(string test)
        {
            return FormattedMessage.RemoveMarkup(test);
        }

        [Test]
        [TestCase("Foo")]
        [TestCase("[color=#FF000000]Foo[/color]")]
        [TestCase("[color=#00FF00FF]Foo[/color]bar")]
        public static void TestToMarkup(string text)
        {
            var message = FormattedMessage.FromMarkup(text);
            Assert.That(message.ToMarkup(), NUnit.Framework.Is.EqualTo(text));
        }

        [Test]
        [TestCase("Foo")]
        [TestCase("[color=#FF000000]Foo[/color]")]
        [TestCase("[color=#00FF00FF]Foo[/color]bar")]
        [TestCase("honk honk [color=#00FF00FF]Foo[/color]bar")]
        public static void TestEnumerateRunes(string text)
        {
            var message = FormattedMessage.FromMarkup(text);

            Assert.That(
                message.EnumerateRunes(),
                Is.EquivalentTo(message.ToString().EnumerateRunes()));
        }
    }
}
