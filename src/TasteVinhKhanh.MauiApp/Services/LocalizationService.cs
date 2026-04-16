using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Service quản lý đa ngôn ngữ toàn app.
/// Khi ngôn ngữ thay đổi → bắn sự kiện LanguageChanged
/// để tất cả ViewModel tự cập nhật text.
/// </summary>
public partial class LocalizationService : ObservableObject
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance!;

    public event Action? LanguageChanged;

    [ObservableProperty]
    private string _currentLanguage = "vi";

    public LocalizationService()
    {
        _instance = this;
        _currentLanguage = Preferences.Get("language", "vi");
    }

    public string GetBestSupportedSystemLanguage()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return lang switch
        {
            "vi" => "vi",
            "en" => "en",
            "zh" => "zh",
            "ko" => "ko",
            "ja" => "ja",
            _ => "en"
        };
    }

    public void SetLanguage(string lang)
    {
        if (CurrentLanguage == lang) return;
        CurrentLanguage = lang;
        Preferences.Set("language", lang);
        LanguageChanged?.Invoke();
    }

    public string T(string key)
    {
        if (_all.TryGetValue(key, out var arr))
        {
            var idx = _langIndex(CurrentLanguage);
            if (idx >= 0 && idx < arr.Length) return arr[idx];
            return arr[0]; // fallback vi
        }
        return key;
    }

    // 0=vi, 1=en, 2=zh, 3=ko, 4=ja
    private static int _langIndex(string lang) => lang switch {
        "vi" => 0, "en" => 1, "zh" => 2, "ko" => 3, "ja" => 4, _ => 0
    };

    // Mỗi key = mảng 5 phần tử [vi, en, zh, ko, ja]
    private static readonly Dictionary<string, string[]> _all = new()
    {
        // ── HOME ──
        ["Home_Welcome"]     = new[] { "Chào mừng bạn",            "Welcome",               "欢迎",                    "환영합니다",            "ようこそ" },
        ["Home_TitleLine1"]  = new[] { "Welcome to",                "Welcome to",            "欢迎来到",                "어서 오세요",           "ようこそ" },
        ["Home_TitleLine2"]  = new[] { "Vinh Khanh",               "Vinh Khanh",           "荣氏街",                  "빈칸 거리",            "ヴィンカイン" },
        ["Home_Subtitle"]    = new[] { "Khám phá phố ẩm thực Vĩnh Khánh. Từ ốc nướng đến lẩu hun khói, cuộc phiêu lưu chợ đêm bắt đầu từ đây.",
                                      "Experience the authentic pulse of Vinh Khanh Street. From sizzling snails to smoky grills, your night market adventure starts here.",
                                      "体验荣氏街的真实脉搏。从香脆蜗牛到烟熏烧烤，您的夜市探险从这里开始。",
                                      "빈칸 거리의 진정한 박동과 함께하세요. 바삭한 달팽이부터 그릴 요리까지, 밤 시장의 모험이 여기서 시작됩니다.",
                                      "ヴィンカイン通りの本当の鼓動を体験してください。サクサクのカタツムリからスモーキーなグリルまで、ナイトマーケットの冒険がここから始まります。" },
        ["Home_StreetEats"]  = new[] { "Vị Ngon",                  "Street Eats",          "街头美食",                "거리 음식",            "ストリートイーツ" },
        ["Home_StreetEatsSub"]= new[] { "Điểm thuyết minh",        "Points of Interest",    "美食亮点",                "주요 명소",            "フォリオ" },
        ["Home_Explore"]     = new[] { "KHÁM PHÁ ĐÊM NAY  →",    "EXPLORE THE NIGHT  →", "探索夜市  →",            "밤 거리 탐험  →",     "ナイトマーケットを探索  →" },
        ["Home_Map"]         = new[] { "Bản đồ",                   "Map",                  "地图",                    "지도",                 "マップ" },
        ["Home_MapSub"]      = new[] { "Tìm quán",                 "Find stalls",          "寻找摊位",                "노점 찾기",            "屋台を探す" },
        ["Home_Audio"]       = new[] { "Thuyết minh",              "Audio Guide",          "语音导览",                "오디오 가이드",        "オーディオガイド" },
        ["Home_AudioSub"]    = new[] { "Câu chuyện phố",           "Street stories",        "街道故事",                "거리 이야기",          "ストリートの物語" },
        ["Home_Tour"]        = new[] { "Tour",                     "Tours",                "导览路线",                "투어",                "ツアー" },
        ["Home_TourSub"]     = new[] { "Lộ trình gợi ý",           "Suggested routes",     "推荐路线",                "추천 경로",            "おすすめルート" },

        // ── MAP ──
        ["Map_Header"]       = new[] { "🍜 Phố Vĩnh Khánh",       "🍜 Vinh Khanh Street", "🍜 荣氏街",             "🍜 빈칸 거리",         "🍜 ヴィンカイン通り" },
        ["Map_PoiListTitle"] = new[] { "Các điểm thuyết minh",    "Points of Interest",    "美食亮点",                "주요 명소",            "フォリオ" },
        ["Map_PoiListSub"]   = new[] { "Vĩnh Khánh, Quận 4, TP.HCM",
                                       "Vinh Khanh, District 4, HCMC",
                                       "荣氏街，第4郡，胡志明市",
                                       "빈칸 거리, 4구역, 호치민시",
                                       "ヴィンカイン通り、4区、ホーチミン市" },
        ["Map_Loading"]      = new[] { "Đang tải dữ liệu...",    "Loading data...",      "正在加载数据...",         "데이터 로딩 중...",    "データを読み込み中..." },
        ["Map_LoadingHint"]  = new[] { "Đảm bảo API đang chạy và có kết nối mạng",
                                       "Make sure the API is running and you have internet",
                                       "确保API正在运行且网络已连接",
                                       "API가 실행 중이고 인터넷에 연결되어 있는지 확인하세요",
                                       "APIが実行中でインターネットに接続していることを確認してください" },
        ["Map_HasNarration"] = new[] { "🎵 Có thuyết minh",      "🎵 Has narration",     "🎵 有语音导览",           "🎵 해설 있음",         "🎵 ナレーションあり" },
        ["Map_NarrationReady"]= new[] { "Sẵn sàng",               "Ready",               "准备就绪",                "준비됨",               "準備完了" },
        ["Map_Playing"]      = new[] { "Đang phát: ",             "Playing: ",           "正在播放：",             "재생 중：",            "再生中：" },
        ["Map_Near"]         = new[] { "Gần: ",                   "Near: ",              "附近：",                  "근처：",              "近く：" },
        ["Map_Syncing"]      = new[] { "Đang đồng bộ dữ liệu...", "Syncing data...",     "正在同步数据...",          "데이터 동기화 중...",  "データを同期中..." },
        ["Map_Synced"]       = new[] { "Đã tải ",                 "Loaded ",             "已加载 ",                 "로드됨 ",             "ロード済み " },
        ["Map_Offline"]      = new[] { "📴 Ngoại tuyến: ",        "📴 Offline: ",         "📴 离线：",              "📴 오프라인：",        "📴 オフライン：" },
        ["Map_NoData"]       = new[] { "❌ Không có điểm nào",   "❌ No points found",   "❌ 未找到任何点",         "❌ 포인트를 찾을 수 없음","❌ ポイントが見つかりません" },

        // ── AUDIO ──
        ["Audio_NowPlaying"] = new[] { "Đang phát",               "Now Playing",         "正在播放",                "지금 재생 중",          "再生中" },
        ["Audio_Select"]     = new[] { "Chọn một điểm thuyết minh","Select a narration point","选择一个导览点",        "내레이션 포인트를 선택하세요","ナレーションポイントを選択してください" },
        ["Audio_SelectHint"] = new[] { "Đi đến bản đồ và chọn một quán để bắt đầu nghe thuyết minh",
                                       "Go to the map and select a stall to start listening",
                                       "前往地图选择一个摊位开始收听导览",
                                       "지도로 이동하여 내레이션을 시작할 축제를 선택하세요",
                                       "マップに移動してナレーションを聴く屋台を選択してください" },
        ["Audio_Stall"]      = new[] { "QUÁN #",                  "STALL #",              "摊位 #",                  "매장 #",               "屋台 #" },

        // ── SETTINGS ──
        ["Settings_Header"]          = new[] { "Cài đặt",            "Settings",            "设置",                    "설정",                "設定" },
        ["Settings_Subtitle"]         = new[] { "Tùy chỉnh trải nghiệm chợ đêm của bạn",
                                                "Customize your night market experience",
                                                "自定义您的夜市体验",
                                                "밤 시장 경험을 사용자 지정하세요",
                                                "ナイトマーケット体験をカスタマイズ" },
        ["Settings_Language"]         = new[] { "🌐  Ngôn ngữ",      "🌐  Language",       "🌐  语言",              "🌐  언어",            "🌐  言語" },
        ["Settings_Native"]           = new[] { "Ngôn ngữ bản địa",  "Native language",    "母语",                    "모국어",              "母国語" },
        ["Settings_International"]    = new[] { "Tiêu chuẩn quốc tế","International Standard","国际标准",              "국제 표준",            "国際標準" },
        ["Settings_ChineseSimplified"]= new[] { "Trung Quốc giản thể","Chinese Simplified","简体中文",              "중국어 간체",          "中国簡体字" },
        ["Settings_Korean"]          = new[] { "Hàn Quốc",           "Korean",              "韩语",                    "한국어",              "韓国語" },
        ["Settings_Japanese"]         = new[] { "Nhật Bản",           "Japanese",            "日语",                    "일본어",              "日本語" },
        ["Settings_AudioFeatures"]    = new[] { "🔊  Tính năng âm thanh","🔊  Audio Features","🔊  音频功能",          "🔊  오디오 기능",      "🔊  オーディオ機能" },
        ["Settings_AutoPlay"]         = new[] { "Tự động phát thuyết minh","Auto-play Narration","自动播放语音导览",     "자동 내레이션 재생",    "自動ナレーション再生" },
        ["Settings_AutoPlayHint"]     = new[] { "Nghe thuyết minh khi đi ngang quán ẩm thực",
                                                "Hear descriptions as you walk by food stalls",
                                                "经过美食摊位时自动收听语音导览",
                                                "음식 축제 옆을 걸을 때 해설을 들으세요",
                                                "屋台の横を通る時に自動再生" },
        ["Settings_Display"]          = new[] { "🖥  Hiển thị",      "🖥  Display",         "🖥  显示",              "🖥  디스플레이",       "🖥  表示" },
        ["Settings_TextSizeSmall"]   = new[] { "Nhỏ",               "Small",               "小",                      "작게",                "小" },
        ["Settings_TextSizeLarge"]    = new[] { "Cỡ lớn",            "Large Text",          "大字体",                  "큰 글꼴",              "大きな文字" },
        ["Settings_Contrast"]         = new[] { "Chế độ tương phản cao","High Contrast Mode","高对比度模式",           "고대비 모드",           "ハイコントラストモード" },
        ["Settings_Help"]             = new[] { "Cần hỗ trợ?",        "Need Help?",          "需要帮助？",              "도움이 필요하세요?",   "ヘルプが必要ですか？" },
        ["Settings_HelpHint"]          = new[] { "Hướng dẫn viên phố ẩm thực hỗ trợ 24/7",
                                                "Talk to our street guides available 24/7",
                                                "我们的街道导游全天候为您提供帮助",
                                                "연중무휴 24시간 이용 가능한 거리 가이드와 대화하세요",
                                                "24時間対応のストリートガイドがサポート" },
        ["Settings_Version"]          = new[] { "PHỐ VĨNH KHÁNH V1.0","VINH KHANH STREET V1.0","荣氏街 V1.0",       "빈칸 거리 V1.0",       "ヴィンカイン通り V1.0" },
        ["Settings_VersionSub"]       = new[] { "Thiết kế cho những con phố Q4",
                                                "Crafted for the streets of Q4",
                                                "为第4郡的街道而打造",
                                                "4구역 거리를 위해 제작됨",
                                                "4区のストリートのために作られた" },

        // ── NAV ──
        ["Nav_Home"]     = new[] { "Trang chủ", "Home",  "首页",    "홈",     "ホーム" },
        ["Nav_Map"]      = new[] { "Bản đồ",   "Map",   "地图",    "지도",   "マップ" },
        ["Nav_Audio"]    = new[] { "Âm thanh",  "Audio", "音频",    "오디오", "オーディオ" },
        ["Nav_Favorites"] = new[] { "Yêu thích", "Favorites", "收藏", "즐겨찾기", "お気に入り" },
        ["Nav_Settings"]  = new[] { "Cài đặt",   "Settings","设置",  "설정",   "設定" },
        ["Tour_Header"]   = new[] { "🧭 Tour ẩm thực", "🧭 Food Tours", "🧭 美食导览", "🧭 푸드 투어", "🧭 フードツアー" },
        ["Tour_Subtitle"] = new[] { "Lộ trình khám phá Vĩnh Khánh", "Explore routes on Vinh Khanh", "探索荣氏街路线", "빈칸 탐방 경로", "ヴィンカイン探索ルート" },
        ["Tour_Empty"]    = new[] { "Chưa có tour nào", "No tours yet", "暂无导览路线", "투어가 없습니다", "ツアーはまだありません" },

        // ── FAVORITES PAGE ──
        ["Fav_Header"]   = new[] { "❤️  Yêu thích",     "❤️  Favorites",      "❤️  收藏",            "❤️  즐겨찾기",          "❤️  お気に入り" },
        ["Fav_Title"]    = new[] { "Quán yêu thích",    "Favorite Stalls",    "收藏摊位",             "즐겨찾기 매장",          "お気に入り屋台" },
        ["Fav_Subtitle"] = new[] { "Những quán bạn đã thích",
                                  "Stalls you've liked",
                                  "您收藏的摊位",
                                  "좋아하는 매장",
                                  "お気に入りに追加した屋台" },
        ["Fav_Empty"]    = new[] { "Chưa có quán yêu thích", "No favorites yet", "暂无收藏", "아직 즐겨찾기 없음", "お気に入りはまだありません" },
        ["Fav_EmptyHint"]= new[] { "Bấm ❤️ trên bản đồ để thêm quán vào danh sách",
                                  "Tap ❤️ on the map to add stalls",
                                  "点击地图上的❤️添加摊位到收藏",
                                  "지도에서 ❤️를 탭하여 매장을 추가하세요",
                                  "マップで❤️をタップして屋台を追加してください" },

        // ── POI DETAIL ──
        ["Poi_Location"]      = new[] { "Địa điểm",         "Location",            "地点",               "위치",              "所在地" },
        ["Poi_Narration"]     = new[] { "Thuyết minh",       "Narration",          "解说",               "해설",              "ナレーション" },
        ["Poi_Favorite"]      = new[] { "Yêu thích",         "Favorite",           "收藏",               "즐겨찾기",           "お気に入り" },
        ["Poi_LocationTitle"] = new[] { "📍 Địa điểm",      "📍 Location",        "📍 地点",            "📍 위치",           "📍 所在地" },
        ["Poi_OpenMap"]       = new[] { "🗺  Mở Google Maps",   "🗺  Open Google Maps",    "🗺  打开Google地图",  "🗺  Google지도 열기",  "🗺  Googleマップを開く" },
        ["Poi_PlayNarration"] = new[] { "🎙  Nghe thuyết minh", "🎙  Listen to narration", "🎙  收听解说",        "🎙  해설 듣기",        "🎙  ナレーションを聴く" },
        ["Poi_NarrationTitle"]= new[] { "🎵  Kịch bản thuyết minh", "🎵  Narration script", "🎵  解说脚本",   "🎵  해설 스크립트",     "🎵  ナレーションスクリプト" },
        ["Poi_AvailableLangs"]= new[] { "Ngôn ngữ có sẵn:",   "Available languages:", "可用语言：",         "사용 가능한 언어：",    "利用可能な言語：" },
        ["Poi_FavoriteTitle"] = new[] { "⭐  Yêu thích",      "⭐  Favorite",       "⭐  收藏",            "⭐  즐겨찾기",         "⭐  お気に入り" },
        ["Poi_FavoriteAdd"]   = new[] { "❤️  Thêm vào yêu thích", "❤️  Add to favorites", "❤️  添加到收藏",  "❤️  즐겨찾기에 추가",    "❤️  お気に入りに追加" },
        ["Poi_LangLabel"]     = new[] { "Ngôn ngữ thuyết minh","Narration language", "解说语言",             "해설 언어",            "ナレーション言語" },
    };
}
