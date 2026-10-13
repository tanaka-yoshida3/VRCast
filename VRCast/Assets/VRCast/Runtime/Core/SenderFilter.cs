using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace VRCast.Core
{
    /// <summary>
    /// 外部アプリ（VMC・iFacialMocap）から LAN 経由で届くパケットを、決めた送り主（IP アドレス）からだけ受け取るための判定。
    /// この PC（ループバック）からはいつでも受け取る（同じ PC で動かす VirtualMotionCapture 等を止めないため）。
    /// </summary>
    public static class SenderFilter
    {
        // 送り主の区切り（カンマ・セミコロン・空白・改行・読点）
        private static readonly char[] Separators = { ',', ';', ' ', '\t', '\r', '\n', '、' };

        /// <summary>
        /// 送り主の一覧の文字列を IPv4 / IPv6 アドレスへ解析する。読めなかった項目は invalid（null 可）へ足す。
        /// </summary>
        public static List<IPAddress> Parse(string text, List<string> invalid = null)
        {
            var result = new List<IPAddress>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            foreach (string part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                // 4 つの数字の IPv4 と IPv6 だけを受け付ける（"192.168" のような省略形は IPAddress が別のアドレスに読むため弾く）
                if (IPAddress.TryParse(part, out IPAddress address) && IsWellFormed(part, address))
                {
                    address = Normalize(address);
                    if (!result.Contains(address))
                    {
                        result.Add(address);
                    }
                }
                else
                {
                    invalid?.Add(part);
                }
            }

            return result;
        }

        /// <summary>
        /// sender から受け取ってよいなら true（この PC からはいつでも、それ以外は allowed に含まれるときだけ）。
        /// </summary>
        public static bool IsAllowed(IPAddress sender, IReadOnlyList<IPAddress> allowed)
        {
            if (sender == null)
            {
                return false;
            }

            sender = Normalize(sender);
            if (IPAddress.IsLoopback(sender))
            {
                return true;
            }

            if (allowed == null)
            {
                return false;
            }

            for (int i = 0; i < allowed.Count; i++)
            {
                if (sender.Equals(allowed[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 一覧の文字列 text に address を足した文字列を返す（すでにあればそのまま）。
        /// </summary>
        public static string Add(string text, string address)
        {
            text = (text ?? string.Empty).Trim();
            if (!IPAddress.TryParse(address, out IPAddress parsed))
            {
                return text;
            }

            if (Parse(text).Contains(Normalize(parsed)))
            {
                return text;
            }

            return text.Length == 0 ? address : text + ", " + address;
        }

        /// <summary>
        /// IPv6 に埋め込まれた IPv4（::ffff:192.168.1.20）を IPv4 にそろえる（同じ送り主を同じ値で比べるため）。
        /// </summary>
        public static IPAddress Normalize(IPAddress address)
        {
            return address != null && address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6
                ? address.MapToIPv4()
                : address;
        }

        private static bool IsWellFormed(string text, IPAddress address)
        {
            // IPv4 は「数字 4 つをドットでつないだ形」のときだけ（IPv6 はそのまま認める）
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return true;
            }

            string[] parts = text.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 3)
                {
                    return false;
                }

                foreach (char c in part)
                {
                    if (c < '0' || c > '9')
                    {
                        return false;
                    }
                }

                if (int.Parse(part) > 255)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
