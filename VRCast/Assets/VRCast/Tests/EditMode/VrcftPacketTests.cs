using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRCast.Remote;
using VRCast.Tracking;
using static VRCast.Tests.OscTestData;

namespace VRCast.Tests
{
    /// <summary>
    /// VRCFaceTracking の受信（float だけ使う・値の保持・ARKit への写し方・左右・合図のメッセージ）を検証する。
    /// </summary>
    public class VrcftPacketTests
    {
        [Test]
        public void ForceRelevantMessage_IsAnOscTrue()
        {
            // VRCFaceTracking が読める OSC の true であること
            var messages = new List<OscMessage>();
            byte[] bytes = VrcftPacket.ForceRelevantMessage;
            OscPacket.Parse(bytes, 0, bytes.Length, messages);

            Assert.That(bytes.Length % 4, Is.EqualTo(0));
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Address, Is.EqualTo(VrcftPacket.ForceRelevantAddress));
            Assert.That(messages[0].Kind, Is.EqualTo(OscValueKind.True));
        }

        [Test]
        public void Read_FloatParameter_BuildsFrame()
        {
            var vrcft = new VrcftPacket();

            Assert.That(vrcft.Read(Bundle(Param("JawOpen", 0.6f), Param("MouthSmileLeft", 0.5f)), out bool hasFrame,
                out FaceTrackingFrame frame), Is.True);
            Assert.That(hasFrame, Is.True);
            Assert.That(frame.MouthOpen, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(frame.BlendShapes, Is.Not.Null);
            Assert.That(frame.HasExpression, Is.True);
            Assert.That(frame.HeadRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(vrcft.ReceivedCount, Is.EqualTo(2));
        }

        [Test]
        public void Read_IgnoresBoolAndBinaryCopies()
        {
            // 同じ名前の bool・2 進数（名前 + 1 等）は値として使わない（VRCFaceTracking のパケットとしては正常）
            var vrcft = new VrcftPacket();
            byte[] packet = Bundle(
                Message("/avatar/parameters/v2/JawOpen", ",T"),
                Message("/avatar/parameters/v2/JawOpen1", ",F"));

            Assert.That(vrcft.Read(packet, out bool hasFrame, out _), Is.True);
            Assert.That(hasFrame, Is.False);
            Assert.That(vrcft.ReceivedCount, Is.EqualTo(0));
        }

        [Test]
        public void Read_KeepsValuesAcrossPackets()
        {
            // 変わった値だけが届くため、前のパケットの値を持ち続ける
            var vrcft = new VrcftPacket();
            vrcft.Read(Param("MouthSmileLeft", 0.8f), out _, out _);
            vrcft.Read(Param("JawOpen", 0.3f), out bool hasFrame, out _);

            float[] scores = vrcft.BuildPersonScores();
            Assert.That(hasFrame, Is.True);
            Assert.That(scores[ArKitFace.IndexOf("mouthSmileLeft")], Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("jawOpen")], Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void Read_OtherOscIsNotVrcft()
        {
            // VRCFaceTracking のアドレスを含まなければ不正なパケット
            var vrcft = new VrcftPacket();
            Assert.That(vrcft.Read(Message("/VMC/Ext/Blend/Apply", ","), out _, out _), Is.False);
            Assert.That(vrcft.Read(new byte[] { 1, 2, 3, 4 }, out _, out _), Is.False);
        }

        [Test]
        public void BuildPersonScores_AveragesPairsAndMapsLips()
        {
            var vrcft = new VrcftPacket();
            vrcft.Read(Bundle(
                Param("BrowInnerUpLeft", 0.2f), Param("BrowInnerUpRight", 0.6f),
                Param("LipPuckerUpperLeft", 1f), Param("LipPuckerUpperRight", 1f),
                Param("MouthRaiserLower", 0.7f)), out _, out _);

            float[] scores = vrcft.BuildPersonScores();
            Assert.That(scores[ArKitFace.IndexOf("browInnerUp")], Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("mouthPucker")], Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("mouthShrugLower")], Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void BuildPersonScores_BlinkFromOpennessOrLid()
        {
            // 開き具合があればそれを、無ければまぶた（0.75 で開き切り）から求める
            var vrcft = new VrcftPacket();
            vrcft.Read(Bundle(Param("EyeOpenLeft", 0.2f), Param("EyeLidRight", 0.375f)), out _, out _);

            float[] scores = vrcft.BuildPersonScores();
            Assert.That(scores[ArKitFace.IndexOf("eyeBlinkLeft")], Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("eyeBlinkRight")], Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void BuildPersonScores_GazeToTheRightIsLeftInRightOut()
        {
            // 本人の右を見ると、左目は内寄せ・右目は外寄せ。上を見ると両目とも上
            var vrcft = new VrcftPacket();
            vrcft.Read(Bundle(Param("EyeLeftX", 0.25f), Param("EyeRightX", 0.25f), Param("EyeLeftY", 0.1f),
                Param("EyeRightY", 0.1f)), out _, out _);

            float[] scores = vrcft.BuildPersonScores();
            Assert.That(scores[ArKitFace.IndexOf("eyeLookInLeft")], Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("eyeLookOutRight")], Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("eyeLookOutLeft")], Is.EqualTo(0f));
            Assert.That(scores[ArKitFace.IndexOf("eyeLookUpLeft")], Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(scores[ArKitFace.IndexOf("eyeLookUpRight")], Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void BuildFrame_KeepsThePersonsLeftEye()
        {
            // iFacialMocap と同じく本人基準の左右で届くので、本人の左目を閉じるとフレームの左目（本人から見た左）が閉じ、
            // パーフェクトシンク用の値（映像基準）では eyeBlinkRight 側に入る
            var vrcft = new VrcftPacket();
            vrcft.Read(Bundle(Param("EyeOpenLeft", 0f), Param("EyeOpenRight", 1f)), out _, out FaceTrackingFrame frame);

            Assert.That(frame.EyeOpenLeft, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(frame.EyeOpenRight, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(frame.BlendShapes[ArKitFace.EyeBlinkRight], Is.EqualTo(1f).Within(1e-5f));
        }

        private static byte[] Param(string name, float value)
        {
            return Message(VrcftPacket.ParameterPrefix + name, ",f", Float(value));
        }
    }

    /// <summary>
    /// テストから読みやすく呼ぶための拡張（パケット全体を読む）。
    /// </summary>
    internal static class VrcftPacketTestExtensions
    {
        public static bool Read(this VrcftPacket packet, byte[] bytes, out bool hasFrame, out FaceTrackingFrame frame)
        {
            return packet.Read(bytes, bytes.Length, out hasFrame, out frame);
        }
    }
}
