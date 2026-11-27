using System.Collections.Generic;
using UnityEngine;

namespace Manimal.LoadAmmoAnim
{
    // one entry per clip set in the bundle, and which magazines play it. written by gen_families.py from the
    // unity pose dumps: the seat is where each set leaves the round, measured against its reference magazine.
    internal sealed partial class MagAnimFamily
    {
        public static readonly MagAnimFamily Stanag556 = new MagAnimFamily
        {
            Key = "stanag556",
            StateSuffix = "",
            SeatPos = new Vector3(-0.00403f, 0.09227f, -0.02527f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00951f,
        };

        public static readonly MagAnimFamily Ak545 = new MagAnimFamily
        {
            Key = "ak545",
            StateSuffix = " ak545",
            SeatPos = new Vector3(0.00343f, 0.10285f, -0.02459f),
            SeatRot = new Quaternion(-0.00053f, 0f, 0f, 1f),
            ShiftTravel = 0.00945f,
        };

        public static readonly MagAnimFamily Stanag10 = new MagAnimFamily
        {
            Key = "stanag10",
            StateSuffix = " stanag10",
            SeatPos = new Vector3(-0.00403f, 0.09136f, -0.02532f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00951f,
        };

        public static readonly MagAnimFamily Pmag20 = new MagAnimFamily
        {
            Key = "pmag20",
            StateSuffix = " pmag20",
            SeatPos = new Vector3(-0.00403f, 0.09136f, -0.02532f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00951f,
        };

        public static readonly MagAnimFamily Pmag40 = new MagAnimFamily
        {
            Key = "pmag40",
            StateSuffix = " pmag40",
            SeatPos = new Vector3(-0.00403f, 0.0908f, -0.02535f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00951f,
        };

        public static readonly MagAnimFamily Stanag60 = new MagAnimFamily
        {
            Key = "stanag60",
            StateSuffix = " stanag60",
            SeatPos = new Vector3(-0.00465f, 0.09269f, -0.02524f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00966f,
        };

        public static readonly MagAnimFamily Stanag100 = new MagAnimFamily
        {
            Key = "stanag100",
            StateSuffix = " stanag100",
            SeatPos = new Vector3(-0.00465f, 0.09269f, -0.02524f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00966f,
        };

        public static readonly MagAnimFamily D60 = new MagAnimFamily
        {
            Key = "d60",
            StateSuffix = " d60",
            SeatPos = new Vector3(-0.00403f, 0.09227f, -0.02527f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00949f,
        };

        public static readonly MagAnimFamily Aug30 = new MagAnimFamily
        {
            Key = "aug30",
            StateSuffix = " aug30",
            SeatPos = new Vector3(-0.00403f, 0.1012f, 0.00494f),
            SeatRot = new Quaternion(0.00681f, 0f, -0.0005f, 0.99998f),
            ShiftTravel = 0.01025f,
        };

        public static readonly MagAnimFamily Sg550 = new MagAnimFamily
        {
            Key = "sg550",
            StateSuffix = " sg550",
            SeatPos = new Vector3(0.00432f, 0.09124f, -0.02444f),
            SeatRot = new Quaternion(0.01425f, 0f, 0f, 0.9999f),
            ShiftTravel = 0.0096f,
        };

        public static readonly MagAnimFamily Ak101 = new MagAnimFamily
        {
            Key = "ak101",
            StateSuffix = " ak101",
            SeatPos = new Vector3(0.00343f, 0.1036f, -0.02396f),
            SeatRot = new Quaternion(0.01345f, 0f, 0f, 0.99991f),
            ShiftTravel = 0.00942f,
        };

        public static readonly MagAnimFamily Cwp = new MagAnimFamily
        {
            Key = "cwp",
            StateSuffix = " cwp",
            SeatPos = new Vector3(0.00343f, 0.10568f, -0.02662f),
            SeatRot = new Quaternion(0.00058f, 0f, 0f, 1f),
            ShiftTravel = 0.00934f,
        };

        public static readonly MagAnimFamily Saiga10 = new MagAnimFamily
        {
            Key = "saiga10",
            StateSuffix = " saiga10",
            SeatPos = new Vector3(0.00343f, 0.1042f, -0.02516f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00941f,
        };

        public static readonly MagAnimFamily Ak12 = new MagAnimFamily
        {
            Key = "ak12",
            StateSuffix = " ak12",
            SeatPos = new Vector3(-0.00448f, 0.10524f, -0.02144f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00951f,
        };

        public static readonly MagAnimFamily Ak45 = new MagAnimFamily
        {
            Key = "ak45",
            StateSuffix = " ak45",
            SeatPos = new Vector3(0.00343f, 0.1042f, -0.02516f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00941f,
        };

        public static readonly MagAnimFamily Ak60 = new MagAnimFamily
        {
            Key = "ak60",
            StateSuffix = " ak60",
            SeatPos = new Vector3(0.00343f, 0.10426f, -0.02622f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00941f,
        };

        public static readonly MagAnimFamily Rpk95 = new MagAnimFamily
        {
            Key = "rpk95",
            StateSuffix = " rpk95",
            SeatPos = new Vector3(-0.00443f, 0.09839f, -0.02199f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00975f,
        };

        public static readonly MagAnimFamily Cmag100 = new MagAnimFamily
        {
            Key = "cmag100",
            StateSuffix = " cmag100",
            SeatPos = new Vector3(0.00394f, 0.09252f, -0.02452f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00953f,
        };

        public static readonly MagAnimFamily Glock17 = new MagAnimFamily
        {
            Key = "glock17",
            StateSuffix = " glock17",
            SeatPos = new Vector3(-0.00144f, 0.12467f, 0.02081f),
            SeatRot = new Quaternion(0.01468f, 0f, 0f, 0.99989f),
            ShiftTravel = 0.01022f,
        };

        public static readonly MagAnimFamily Glock33 = new MagAnimFamily
        {
            Key = "glock33",
            StateSuffix = " glock33",
            SeatPos = new Vector3(-0.00014f, 0.12467f, 0.02081f),
            SeatRot = new Quaternion(0.01468f, 0f, 0f, 0.99989f),
            ShiftTravel = 0.00929f,
        };

        public static readonly MagAnimFamily Glock50 = new MagAnimFamily
        {
            Key = "glock50",
            StateSuffix = " glock50",
            SeatPos = new Vector3(0.00041f, 0.12428f, 0.02247f),
            SeatRot = new Quaternion(0.01468f, 0f, 0f, 0.99989f),
            ShiftTravel = 0.01003f,
        };

        public static readonly MagAnimFamily M9a3 = new MagAnimFamily
        {
            Key = "m9a3",
            StateSuffix = " m9a3",
            SeatPos = new Vector3(0.00034f, 0.13953f, 0.02798f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00961f,
        };

        public static readonly MagAnimFamily M1911 = new MagAnimFamily
        {
            Key = "m1911",
            StateSuffix = " m1911",
            SeatPos = new Vector3(-0.00016f, 0.09984f, 0.00445f),
            SeatRot = new Quaternion(-0.01579f, 0f, 0f, 0.99988f),
            ShiftTravel = 0.01227f,
        };

        public static readonly MagAnimFamily Fn57 = new MagAnimFamily
        {
            Key = "fn57",
            StateSuffix = " fn57",
            SeatPos = new Vector3(-0.00401f, 0.14328f, 0.03281f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.00787f,
        };

        public static readonly MagAnimFamily Deagle = new MagAnimFamily
        {
            Key = "deagle",
            StateSuffix = " deagle",
            SeatPos = new Vector3(-0.00016f, 0.1486f, 0.02857f),
            SeatRot = new Quaternion(0.02904f, 0f, 0f, 0.99958f),
            ShiftTravel = 0.01399f,
        };

        // magazine template id -> its set. anything not listed plays the stanag one.
        public static readonly Dictionary<string, MagAnimFamily> ByTemplate = new Dictionary<string, MagAnimFamily>
        {
            ["55d480c04bdc2d1d4e8b456a"] = Ak545, // 6l23
            ["5cbdaf89ae9215000e5b9c94"] = Ak545, // 6l23 plum
            ["564ca99c4bdc2d16268b4589"] = Ak545, // 6l20
            ["5aaa4194e5b5b055d06310a5"] = Ak545, // pmag 30 ak74
            ["5aaa5e60e5b5b000140293d6"] = Stanag10, // pmag 10
            ["5448c1d04bdc2dff2f8b4569"] = Pmag20, // pmag 20
            ["544a378f4bdc2d30388b4567"] = Pmag40, // pmag 40
            ["5d1340bdd7ad1a0e8d245aab"] = Pmag40, // pmag 40 fde
            ["544a37c44bdc2d25388b4567"] = Stanag60, // surefire mag5-60
            ["5c6592372e221600133e47d7"] = Stanag100, // surefire mag5-100
            ["59c1383d86f774290a37e0ca"] = D60, // pmag d-60
            ["62e7c98b550c8218d602cbb4"] = Aug30, // aug 30
            ["630e1adbbd357927e4007c09"] = Aug30, // aug 10
            ["630e295c984633f1fb0e7c30"] = Aug30, // aug 42
            ["62307b7b10d2321fa8741921"] = Sg550, // g36 30
            ["5ac66c5d5acfc4001718d314"] = Ak101, // 6l29 ak-101
            ["6764139c44b3c96e7b0e2f7b"] = Ak101, // molot .223 45
            ["5c0548ae0db834001966a3c2"] = Cwp, // arsenal cwp 30
            ["55d4837c4bdc2d1d4e8b456c"] = Saiga10, // saiga 545 10
            ["64b9e265c94d0d15c5027e35"] = Saiga10, // saiga mk 545 20
            ["5bed61680db834001d2c45ab"] = Ak12, // ak-12 30
            ["649ec30cb013f04a700e60fb"] = Ak12, // ak-12 early 30
            ["564ca9df4bdc2d35148b4569"] = Ak45, // 6l18 45
            ["55d481904bdc2d8c2f8b456a"] = Ak45, // 6l26 45
            ["55d482194bdc2d1d4e8b456b"] = Ak60, // 6l31 60
            ["5bed625c0db834001c062946"] = Rpk95, // rpk-16 95 drum
            ["6761765f1f08ed5e8800b7a6"] = Cmag100, // beta c-mag 100
            ["5a718b548dc32e000d46d262"] = Glock17, // Glock 9x19 17
            ["63076701a987397c0816d21b"] = Glock17, // G19X 19
            ["671d8ac8a3e45c1f59082799"] = Glock17, // G19X 999
            ["5a718da68dc32e000d46d264"] = Glock17, // GL9 21
            ["5fb651b52b1b027b1f50bcff"] = Glock17, // Glock .45 13
            ["5a7ad2e851dfba0016153692"] = Glock33, // Big Stick 33
            ["630767c37d50ff5e8a1ea71a"] = Glock33, // Big Stick 31
            ["630769c4962d0247b029dc60"] = Glock33, // Big Stick 24
            ["676176b762e0497044079f49"] = Glock33, // CX4 30
            ["5fb651dc85f90547f674b6f4"] = Glock33, // G30 MagEx 30
            ["5a718f958dc32e00094b97e7"] = Glock50, // G SGMT 50
            ["55d485be4bdc2d962f8b456f"] = Glock50, // PM drum 84
            ["5cadc2e0ae9215051e1c21e7"] = M9a3, // M9A3 17
            ["56d59948d2720bb7418b4582"] = M9a3, // P226 15
            ["5c920e902e221644f31c3c99"] = M9a3, // P226 20
            ["576a5ed62459771e9c2096cb"] = M9a3, // MP-443 18
            ["671d8617a3e45c1f5908278c"] = M9a3, // MP-443 999
            ["602286df23506e50807090c6"] = M9a3, // PL-15 16
            ["59f99a7d86f7745b134aa97b"] = M9a3, // SR-1MP 18
            ["5a17fb03fcdbcbcae668728f"] = M9a3, // APS 20
            ["6193d338de3cdf1d2614a6fc"] = M9a3, // USP45 12
            ["6193d3149fb0c665d5490e32"] = M9a3, // USP45T 12
            ["671d8b8c0959c721a50ca838"] = M9a3, // USP45T 999
            ["5ef3448ab37dfd6af863525c"] = M1911, // Mec-Gar 11
            ["5e81c4ca763d9f754677befa"] = M1911, // 1911 7
            ["5f3e77b26cda304dcc634057"] = M1911, // Wilson 7
            ["671d8b38b769f0d88c0950f8"] = M1911, // Wilson 999
            ["571a29dc2459771fb2755a6a"] = M1911, // tt-105 8
            ["671d85439ae8365d69117ba6"] = M1911, // tt-105 999
            ["5448c12b4bdc2d02308b456f"] = M1911, // PM 8
            ["670e8eab8c1bb0e5a7075acf"] = M1911, // PM 999
            ["5d3eb5eca4b9363b1f22f8e4"] = Fn57, // 5-7 20
            ["668fe5c5f35310705d02b696"] = Deagle, // DE 7
            ["669fa435803b94fb5d0e3a76"] = Deagle, // DE 9
        };
    }
}
