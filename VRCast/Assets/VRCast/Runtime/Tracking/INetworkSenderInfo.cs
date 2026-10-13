namespace VRCast.Tracking
{
    /// <summary>
    /// 外部アプリ（VMC・iFacialMocap）の受信口に届いた送り主の情報（UI で送り主のしぼり込みを設定するため）。
    /// </summary>
    public interface INetworkSenderInfo
    {
        /// <summary>
        /// 最後にパケットが届いた送り主の IP アドレス（受け取ったかどうかは問わない。まだ無ければ空）。
        /// </summary>
        string LastSender { get; }

        /// <summary>
        /// 送り主のしぼり込みで最後に捨てた送り主の IP アドレス（無ければ空）。
        /// </summary>
        string LastRejectedSender { get; }

        /// <summary>
        /// 待ち受けを始めてから、送り主のしぼり込みで捨てたパケットの数。
        /// </summary>
        int RejectedPackets { get; }
    }
}
