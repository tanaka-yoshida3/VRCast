using System.Collections.Generic;
using System.Net;
using NUnit.Framework;
using VRCast.Core;

namespace VRCast.Tests
{
    /// <summary>
    /// 外部アプリの送り主のしぼり込み（SenderFilter）を検証する。
    /// </summary>
    public class SenderFilterTests
    {
        [Test]
        public void Parse_AcceptsCommonSeparatorsAndSkipsDuplicates()
        {
            // カンマ・空白・改行・読点のどれで区切っても読め、同じアドレスは 1 つにまとめる
            var invalid = new List<string>();
            List<IPAddress> result = SenderFilter.Parse("192.168.1.20, 192.168.1.21\n192.168.1.22、192.168.1.20", invalid);

            Assert.That(result, Is.EqualTo(new[]
            {
                IPAddress.Parse("192.168.1.20"), IPAddress.Parse("192.168.1.21"), IPAddress.Parse("192.168.1.22"),
            }));
            Assert.That(invalid, Is.Empty);
        }

        [Test]
        public void Parse_ReportsEntriesThatAreNotFullAddresses()
        {
            // 省略形（IPAddress は "192.168" を別のアドレスとして読む）や文字は読めなかった項目として返す
            var invalid = new List<string>();
            List<IPAddress> result = SenderFilter.Parse("192.168, iphone, 192.168.1.256, 10.0.0.5", invalid);

            Assert.That(result, Is.EqualTo(new[] { IPAddress.Parse("10.0.0.5") }));
            Assert.That(invalid, Is.EqualTo(new[] { "192.168", "iphone", "192.168.1.256" }));
        }

        [Test]
        public void Parse_EmptyTextIsAnEmptyList()
        {
            Assert.That(SenderFilter.Parse(null), Is.Empty);
            Assert.That(SenderFilter.Parse("  "), Is.Empty);
        }

        [Test]
        public void IsAllowed_OnlyListedSenders()
        {
            List<IPAddress> allowed = SenderFilter.Parse("192.168.1.20");

            Assert.That(SenderFilter.IsAllowed(IPAddress.Parse("192.168.1.20"), allowed), Is.True);
            Assert.That(SenderFilter.IsAllowed(IPAddress.Parse("192.168.1.99"), allowed), Is.False);
            Assert.That(SenderFilter.IsAllowed(null, allowed), Is.False);
        }

        [Test]
        public void IsAllowed_ThisPcIsAlwaysAllowed()
        {
            // 同じ PC で動かすアプリ（127.0.0.1）は一覧が空でも受け取る
            var empty = new List<IPAddress>();

            Assert.That(SenderFilter.IsAllowed(IPAddress.Loopback, empty), Is.True);
            Assert.That(SenderFilter.IsAllowed(IPAddress.IPv6Loopback, empty), Is.True);
            Assert.That(SenderFilter.IsAllowed(IPAddress.Parse("192.168.1.20"), empty), Is.False);
        }

        [Test]
        public void IsAllowed_TreatsIpv4MappedAddressesAsIpv4()
        {
            // ::ffff:192.168.1.20 で届いても、一覧の 192.168.1.20 と同じ送り主として扱う
            List<IPAddress> allowed = SenderFilter.Parse("192.168.1.20");

            Assert.That(SenderFilter.IsAllowed(IPAddress.Parse("::ffff:192.168.1.20"), allowed), Is.True);
        }

        [Test]
        public void Add_AppendsOnlyNewAddresses()
        {
            Assert.That(SenderFilter.Add("", "192.168.1.20"), Is.EqualTo("192.168.1.20"));
            Assert.That(SenderFilter.Add("192.168.1.20", "192.168.1.21"), Is.EqualTo("192.168.1.20, 192.168.1.21"));
            Assert.That(SenderFilter.Add("192.168.1.20", "192.168.1.20"), Is.EqualTo("192.168.1.20"));
            Assert.That(SenderFilter.Add("192.168.1.20", "not an address"), Is.EqualTo("192.168.1.20"));
        }
    }
}
