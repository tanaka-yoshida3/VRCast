using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRCast.Remote;

namespace VRCast.Tracking
{
    /// <summary>
    /// VRCFaceTracking（Quest Pro・Pico 4 Pro・Vive Facial Tracker 等の顔と目の値を VRChat へ OSC で送るアプリ）の出力を
    /// FaceTrackingFrame にまとめる。
    /// - VRCFaceTracking は本来 VRChat のアバターが持つパラメーターだけを送るため、受信側から "/vrcft/settings/forceRelevant"（true）を
    ///   VRCFaceTracking の受信ポートへ送り、すべてのパラメーターを送らせる（VRChat を起動していなくても届く）
    /// - "/avatar/parameters/v2/名前 (f 値)": Unified Expressions の値（変わったものだけ届くため、値は受信のたびに上書きして保持する）。
    ///   同じ名前で bool・2 進数（名前 + 1/2/4/8・Negative）も届くが、float だけを使う
    /// - 頭の向きは送られない（VMC の体と組み合わせるときは、VMC 側の頭の向きを使う）
    /// Unified Expressions の左右は本人基準（ARKit と同じ）なので、ARKit の 51 種へ写してから共通の映像基準へ入れ替える。
    /// </summary>
    public sealed class VrcftPacket
    {
        /// <summary>
        /// 使う値のアドレスの先頭（VRChat のアバターのパラメーター + Unified Expressions v2）。
        /// </summary>
        public const string ParameterPrefix = "/avatar/parameters/v2/";

        /// <summary>
        /// すべてのパラメーターを送らせる合図のアドレス（VRCFaceTracking が外部ツール向けに用意している）。
        /// </summary>
        public const string ForceRelevantAddress = "/vrcft/settings/forceRelevant";

        /// <summary>
        /// "/vrcft/settings/forceRelevant" に true を 1 つ載せた OSC メッセージ。
        /// </summary>
        public static readonly byte[] ForceRelevantMessage = BuildBoolMessage(ForceRelevantAddress, true);

        // VRCFaceTracking から届くと分かるアドレスの先頭（値として使わないものも、不正なパケットとは数えない）
        private const string AvatarPrefix = "/avatar/";
        private const string TrackingPrefix = "/tracking/";

        // 視線（Unified Expressions の正規化した向き。0.5 ≒ 30°）を ARKit の eyeLook（1.0 ≒ 30°）へ写す倍率
        private const float GazeScale = 2f;

        // EyeLid（0〜0.75 = 開き具合、0.75〜1 = 見開き）の開き切りの値
        private const float LidOpen = 0.75f;

        /// <summary>
        /// まばたき・口の開きを写す範囲（値はアプリ側で 0〜1 に整えられて届く。実機に合わせて調整する）。
        /// </summary>
        public static readonly ArKitRange Range = new ArKitRange
        {
            BlinkOpen = 0.1f,
            BlinkClosed = 0.85f,
            JawClosed = 0.02f,
            JawOpened = 0.6f,
        };

        // ARKit 名（本人基準）と、その値にする Unified Expressions の名前（複数なら平均）
        private static readonly (string ArKit, string[] Sources)[] ShapeMap =
        {
            ("browDownLeft", new[] { "BrowDownLeft" }),
            ("browDownRight", new[] { "BrowDownRight" }),
            ("browInnerUp", new[] { "BrowInnerUpLeft", "BrowInnerUpRight" }),
            ("browOuterUpLeft", new[] { "BrowOuterUpLeft" }),
            ("browOuterUpRight", new[] { "BrowOuterUpRight" }),
            ("cheekPuff", new[] { "CheekPuffLeft", "CheekPuffRight" }),
            ("cheekSquintLeft", new[] { "CheekSquintLeft" }),
            ("cheekSquintRight", new[] { "CheekSquintRight" }),
            ("eyeSquintLeft", new[] { "EyeSquintLeft" }),
            ("eyeSquintRight", new[] { "EyeSquintRight" }),
            ("eyeWideLeft", new[] { "EyeWideLeft" }),
            ("eyeWideRight", new[] { "EyeWideRight" }),
            ("jawForward", new[] { "JawForward" }),
            ("jawLeft", new[] { "JawLeft" }),
            ("jawOpen", new[] { "JawOpen" }),
            ("jawRight", new[] { "JawRight" }),
            ("mouthClose", new[] { "MouthClosed" }),
            ("mouthDimpleLeft", new[] { "MouthDimpleLeft" }),
            ("mouthDimpleRight", new[] { "MouthDimpleRight" }),
            ("mouthFrownLeft", new[] { "MouthFrownLeft" }),
            ("mouthFrownRight", new[] { "MouthFrownRight" }),
            ("mouthFunnel", new[] { "LipFunnelUpperLeft", "LipFunnelUpperRight", "LipFunnelLowerLeft", "LipFunnelLowerRight" }),
            ("mouthLeft", new[] { "MouthUpperLeft", "MouthLowerLeft" }),
            ("mouthLowerDownLeft", new[] { "MouthLowerDownLeft" }),
            ("mouthLowerDownRight", new[] { "MouthLowerDownRight" }),
            ("mouthPressLeft", new[] { "MouthPressLeft" }),
            ("mouthPressRight", new[] { "MouthPressRight" }),
            ("mouthPucker", new[] { "LipPuckerUpperLeft", "LipPuckerUpperRight", "LipPuckerLowerLeft", "LipPuckerLowerRight" }),
            ("mouthRight", new[] { "MouthUpperRight", "MouthLowerRight" }),
            ("mouthRollLower", new[] { "LipSuckLowerLeft", "LipSuckLowerRight" }),
            ("mouthRollUpper", new[] { "LipSuckUpperLeft", "LipSuckUpperRight" }),
            ("mouthShrugLower", new[] { "MouthRaiserLower" }),
            ("mouthShrugUpper", new[] { "MouthRaiserUpper" }),
            ("mouthSmileLeft", new[] { "MouthSmileLeft" }),
            ("mouthSmileRight", new[] { "MouthSmileRight" }),
            ("mouthStretchLeft", new[] { "MouthStretchLeft" }),
            ("mouthStretchRight", new[] { "MouthStretchRight" }),
            ("mouthUpperUpLeft", new[] { "MouthUpperUpLeft" }),
            ("mouthUpperUpRight", new[] { "MouthUpperUpRight" }),
            ("noseSneerLeft", new[] { "NoseSneerLeft" }),
            ("noseSneerRight", new[] { "NoseSneerRight" }),
        };

        // 目に使う名前（開き具合・まぶた・視線）
        private const string EyeOpenLeft = "EyeOpenLeft";
        private const string EyeOpenRight = "EyeOpenRight";
        private const string EyeLidLeft = "EyeLidLeft";
        private const string EyeLidRight = "EyeLidRight";
        private const string EyeLeftX = "EyeLeftX";
        private const string EyeLeftY = "EyeLeftY";
        private const string EyeRightX = "EyeRightX";
        private const string EyeRightY = "EyeRightY";

        // 保持する名前の一覧（これ以外の名前は覚えない。壊れた送信元で増え続けないように）
        private static readonly HashSet<string> KnownNames = CreateKnownNames();

        // ShapeMap の ARKit 名の位置（ArKitFace.BlendShapeNames 内）と、目の ARKit 名の位置
        private static readonly int[] ShapeIndices = CreateShapeIndices();
        private static readonly int EyeBlinkLeft = ArKitFace.IndexOf("eyeBlinkLeft");
        private static readonly int EyeBlinkRight = ArKitFace.IndexOf("eyeBlinkRight");
        private static readonly int EyeLookInLeft = ArKitFace.IndexOf("eyeLookInLeft");
        private static readonly int EyeLookOutLeft = ArKitFace.IndexOf("eyeLookOutLeft");
        private static readonly int EyeLookUpLeft = ArKitFace.IndexOf("eyeLookUpLeft");
        private static readonly int EyeLookDownLeft = ArKitFace.IndexOf("eyeLookDownLeft");
        private static readonly int EyeLookInRight = ArKitFace.IndexOf("eyeLookInRight");
        private static readonly int EyeLookOutRight = ArKitFace.IndexOf("eyeLookOutRight");
        private static readonly int EyeLookUpRight = ArKitFace.IndexOf("eyeLookUpRight");
        private static readonly int EyeLookDownRight = ArKitFace.IndexOf("eyeLookDownRight");

        // 受信した値（名前 → 最新の値）と、パケットを読む作業領域
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<OscMessage> _messages = new List<OscMessage>();

        /// <summary>
        /// これまでに届いた、使う名前の数（診断用。0 なら顔の値がまだ届いていない）。
        /// </summary>
        public int ReceivedCount => _values.Count;

        /// <summary>
        /// 目の値（開き具合・まぶた・視線のどれか）が届いていれば true（診断用）。
        /// </summary>
        public bool HasEyes => _values.ContainsKey(EyeOpenLeft) || _values.ContainsKey(EyeLidLeft)
            || _values.ContainsKey(EyeLeftX);

        /// <summary>
        /// 保持している値を捨てる（待ち受けの開き直し）。
        /// </summary>
        public void Reset()
        {
            _values.Clear();
        }

        /// <summary>
        /// 1 パケットを読む。VRCFaceTracking のアドレスを 1 つも含まなければ false（不正なパケット）。
        /// 使う値が変わったら hasFrame が true になり、frame に最新の値が入る。
        /// </summary>
        public bool Read(byte[] buffer, int length, out bool hasFrame, out FaceTrackingFrame frame)
        {
            hasFrame = false;
            frame = default;

            _messages.Clear();
            OscPacket.Parse(buffer, 0, length, _messages);
            bool any = false;
            bool changed = false;
            foreach (OscMessage message in _messages)
            {
                string address = message.Address;
                if (address == null)
                {
                    continue;
                }

                // VRCFaceTracking が送るアドレス（アバターのパラメーター・まとめた値）だけを数える
                if (address.StartsWith(AvatarPrefix, StringComparison.Ordinal)
                    || address.StartsWith(TrackingPrefix, StringComparison.Ordinal))
                {
                    any = true;
                }

                // v2 の float で、使う名前のものだけ保持する（同じ名前の bool・2 進数は使わない）
                if (message.Kind != OscValueKind.Float || !TrackingMath.AllFinite(message.Float)
                    || !address.StartsWith(ParameterPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string name = address.Substring(ParameterPrefix.Length);
                if (KnownNames.Contains(name))
                {
                    _values[name] = message.Float;
                    changed = true;
                }
            }

            if (changed)
            {
                hasFrame = true;
                frame = BuildFrame();
            }

            return any;
        }

        /// <summary>
        /// 保持している値から 1 フレームを作る（頭の向きは正面のまま）。
        /// </summary>
        public FaceTrackingFrame BuildFrame()
        {
            var frame = new FaceTrackingFrame { HeadRotation = Quaternion.identity, HeadPosition = Vector3.zero };
            ArKitFace.Fill(ArKitFace.FromPersonSides(BuildPersonScores()), Range, ref frame);
            return frame;
        }

        /// <summary>
        /// 保持している値を、本人基準の左右のままの ARKit の 51 種（ArKitFace.BlendShapeNames 順）へ写す。
        /// </summary>
        public float[] BuildPersonScores()
        {
            var scores = new float[ArKitFace.BlendShapeNames.Length];

            // 名前が対応する形は、そのまま（複数なら平均）
            for (int i = 0; i < ShapeMap.Length; i++)
            {
                string[] sources = ShapeMap[i].Sources;
                float sum = 0f;
                foreach (string source in sources)
                {
                    sum += Value(source);
                }

                scores[ShapeIndices[i]] = Mathf.Clamp01(sum / sources.Length);
            }

            // まばたき = 1 − 開き具合（無ければまぶたの値から。どちらも無ければ開いた目）
            scores[EyeBlinkLeft] = Blink(EyeOpenLeft, EyeLidLeft);
            scores[EyeBlinkRight] = Blink(EyeOpenRight, EyeLidRight);

            // 視線: X は本人の右が正、Y は上が正。左目の右向きは内寄せ、右目の右向きは外寄せ
            float leftX = Value(EyeLeftX) * GazeScale;
            float leftY = Value(EyeLeftY) * GazeScale;
            float rightX = Value(EyeRightX) * GazeScale;
            float rightY = Value(EyeRightY) * GazeScale;
            scores[EyeLookInLeft] = Mathf.Clamp01(leftX);
            scores[EyeLookOutLeft] = Mathf.Clamp01(-leftX);
            scores[EyeLookUpLeft] = Mathf.Clamp01(leftY);
            scores[EyeLookDownLeft] = Mathf.Clamp01(-leftY);
            scores[EyeLookOutRight] = Mathf.Clamp01(rightX);
            scores[EyeLookInRight] = Mathf.Clamp01(-rightX);
            scores[EyeLookUpRight] = Mathf.Clamp01(rightY);
            scores[EyeLookDownRight] = Mathf.Clamp01(-rightY);
            return scores;
        }

        private float Value(string name)
        {
            // 届いていない値は 0（無表情・正面）
            return _values.TryGetValue(name, out float value) ? value : 0f;
        }

        private float Blink(string openName, string lidName)
        {
            if (_values.TryGetValue(openName, out float open))
            {
                return Mathf.Clamp01(1f - open);
            }

            if (_values.TryGetValue(lidName, out float lid))
            {
                return Mathf.Clamp01(1f - lid / LidOpen);
            }

            return 0f;
        }

        private static HashSet<string> CreateKnownNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                EyeOpenLeft, EyeOpenRight, EyeLidLeft, EyeLidRight, EyeLeftX, EyeLeftY, EyeRightX, EyeRightY,
            };
            foreach ((string _, string[] sources) in ShapeMap)
            {
                names.UnionWith(sources);
            }

            return names;
        }

        private static int[] CreateShapeIndices()
        {
            var indices = new int[ShapeMap.Length];
            for (int i = 0; i < ShapeMap.Length; i++)
            {
                indices[i] = ArKitFace.IndexOf(ShapeMap[i].ArKit);
            }

            return indices;
        }

        private static byte[] BuildBoolMessage(string address, bool value)
        {
            // OSC 1.0: アドレス（NUL で 4 バイト境界まで埋める）+ 型タグ ",T" / ",F"（同じく埋める）。引数のデータは無い
            var bytes = new List<byte>();
            AppendPaddedString(bytes, address);
            AppendPaddedString(bytes, value ? ",T" : ",F");
            return bytes.ToArray();
        }

        private static void AppendPaddedString(List<byte> bytes, string text)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(text));
            int padding = 4 - text.Length % 4;
            for (int i = 0; i < padding; i++)
            {
                bytes.Add(0);
            }
        }
    }
}
