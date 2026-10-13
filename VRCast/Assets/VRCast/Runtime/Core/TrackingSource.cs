namespace VRCast.Core
{
    /// <summary>
    /// トラッキングの入力元（settings.json には数値で保存するため並び順を変えず末尾へ足す）。
    /// </summary>
    public enum TrackingSource
    {
        // 同梱の MediaPipe トラッカー（顔・腕・手）
        MediaPipe = 0,

        // 同梱の OpenSeeFace（顔のみ）
        OpenSeeFace = 1,

        // 外部アプリ（iPhone の Waidayo・PC の VirtualMotionCapture 等）が VMC プロトコルで送る顔・全身の値（LAN から受信）
        Vmc = 2,

        // iPhone の iFacialMocap が独自形式で送る顔の値（顔のみ。LAN から受信し、送信開始の合図をこちらから送る）
        IFacialMocap = 3,

        // 併用: 顔は VMC（iPhone 等）、腕・手は同梱の MediaPipe（VMC が途切れている間は MediaPipe の顔）
        MediaPipeVmc = 4,

        // 併用: 顔は iFacialMocap、腕・手は同梱の MediaPipe（iFacialMocap が途切れている間は MediaPipe の顔）
        MediaPipeIFacialMocap = 5,

        // 外部アプリ VRCFaceTracking（Quest Pro 等の顔と目）が OSC で送る値（顔のみ。この PC から受信し、
        // すべての値を送らせる合図をこちらから VRCFaceTracking へ送る）
        VrcFaceTracking = 6,

        // 併用: 体（頭・腕・手・指）は VMC（VirtualMotionCapture 等）、顔は VRCFaceTracking（届かない間は VMC の顔）
        VmcVrcFaceTracking = 7,
    }

    /// <summary>
    /// 入力元ごとにできることの判定（入力元の種類での分岐をここへ集める）。
    /// </summary>
    public static class TrackingSourceInfo
    {
        /// <summary>
        /// 同梱トラッカーそのもの（実行ファイルを同梱する入力元）なら true。併用の入力元は含まない。
        /// </summary>
        public static bool IsBundledTracker(TrackingSource source)
        {
            return source == TrackingSource.MediaPipe || source == TrackingSource.OpenSeeFace;
        }

        /// <summary>
        /// iPhone 等の外部アプリと同梱の MediaPipe を併用する入力元なら true。
        /// </summary>
        public static bool IsHybrid(TrackingSource source)
        {
            return source == TrackingSource.MediaPipeVmc || source == TrackingSource.MediaPipeIFacialMocap;
        }

        /// <summary>
        /// VRCast が同梱トラッカー（カメラ）を起動して受信する入力元なら true（併用の入力元を含む）。
        /// </summary>
        public static bool UsesBundledTracker(TrackingSource source)
        {
            return IsBundledTracker(source) || IsHybrid(source);
        }

        /// <summary>
        /// 起動する同梱トラッカーの入力元（併用は MediaPipe、それ以外はそのまま）。
        /// </summary>
        public static TrackingSource BundledTrackerOf(TrackingSource source)
        {
            return IsHybrid(source) ? TrackingSource.MediaPipe : source;
        }

        /// <summary>
        /// 顔を受け取る外部アプリの入力元（併用は VMC / iFacialMocap、それ以外はそのまま）。
        /// </summary>
        public static TrackingSource NetworkSourceOf(TrackingSource source)
        {
            // 併用の種類ごとに外部アプリ側の入力元を返す
            switch (source)
            {
                case TrackingSource.MediaPipeVmc:
                    return TrackingSource.Vmc;
                case TrackingSource.MediaPipeIFacialMocap:
                    return TrackingSource.IFacialMocap;
                case TrackingSource.VmcVrcFaceTracking:
                    return TrackingSource.Vmc;
                default:
                    return source;
            }
        }

        /// <summary>
        /// 腕・手のトラッキングを受信できる入力元なら true。
        /// </summary>
        public static bool HasArms(TrackingSource source)
        {
            return BundledTrackerOf(source) == TrackingSource.MediaPipe;
        }

        /// <summary>
        /// 全身の姿勢（ボーンごとの回転・体全体の位置）を受信できる入力元なら true。
        /// MediaPipe との併用では腕・手を MediaPipe で動かすため、VMC の全身は使わない（VRCFaceTracking との併用では使う）。
        /// </summary>
        public static bool HasFullBody(TrackingSource source)
        {
            return source == TrackingSource.Vmc || source == TrackingSource.VmcVrcFaceTracking;
        }

        /// <summary>
        /// 顔を VRCFaceTracking から受け取る入力元なら true（VRCFaceTracking だけ・VMC の体との併用）。
        /// </summary>
        public static bool UsesVrcFaceTracking(TrackingSource source)
        {
            return source == TrackingSource.VrcFaceTracking || source == TrackingSource.VmcVrcFaceTracking;
        }

        /// <summary>
        /// ARKit 互換の BlendShape の値（パーフェクトシンク・表情の反映に使う）を受信できる入力元なら true。
        /// </summary>
        public static bool HasArKit(TrackingSource source)
        {
            return source != TrackingSource.OpenSeeFace;
        }

        /// <summary>
        /// 別の PC・スマートフォンから LAN 経由で受信する入力元なら true（127.0.0.1 以外でも待ち受ける）。
        /// 受信口 1 つ分の入力元（VMC・iFacialMocap・同梱トラッカー）について判定する。
        /// </summary>
        public static bool ReceivesFromNetwork(TrackingSource source)
        {
            return source == TrackingSource.Vmc || source == TrackingSource.IFacialMocap;
        }

        /// <summary>
        /// 受信口 1 つ分の入力元の受信ポート（VMC・VRCFaceTracking は設定のポート、iFacialMocap はアプリが決めた固定ポート、
        /// 同梱トラッカーは trackingPort）。
        /// </summary>
        public static int PortOf(AppSettings settings, TrackingSource source)
        {
            switch (source)
            {
                case TrackingSource.Vmc:
                    return settings.vmcPort;
                case TrackingSource.IFacialMocap:
                    return AppSettings.IFacialMocapPort;
                case TrackingSource.VrcFaceTracking:
                    return settings.vrcftPort;
                default:
                    return settings.trackingPort;
            }
        }
    }
}
