using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// キャラの能力値（0〜100、画面ではランク S〜E）。全員が少しずつ伸び、篤の分はループで持ち越す。
    /// 体力：体を使う行事・疲れにくさ　魅力：会話の楽しさ・恋の気持ちの育ち方・学園祭
    /// 器用さ：料理・魔道具・錬成・実技の失敗が減る　魔力：実技の試験と行事・魔力のたまりやすさと使いこなし（剣術・ダンジョン実習は体力）
    /// 知力：覚えのよさ（科目の学力の伸び・課題の進み・勉学の試験）
    /// </summary>
    public static class Abilities
    {
        public const int Body = 0, Charm = 1, Dex = 2, Magic = 3, Mind = 4, Sex = 5, Count = 6;
        public static readonly string[] Names = { "体力", "魅力", "器用さ", "魔力", "知力", "性技" };
        // 性技（2026-10-25 famuru）：エッチの回数と時間で伸びる。高いほど快感ゲージが速くたまる（HSkill.cs）

        public static string Rank(float v)
        {
            if (v >= 85f) return "S";
            if (v >= 70f) return "A";
            if (v >= 55f) return "B";
            if (v >= 40f) return "C";
            if (v >= 25f) return "D";
            return "E";
        }

        /// <summary>ランクの下限（C＝40 など）。</summary>
        public static float RankMin(string r)
        {
            switch (r)
            {
                case "S": return 85f;
                case "A": return 70f;
                case "B": return 55f;
                case "C": return 40f;
                case "D": return 25f;
                default: return 0f;
            }
        }

        /// <summary>科目の試験に効く能力値（座学→知力、剣術・ダンジョン実習→体力、ほかの実技→魔力）。</summary>
        public static int ForSubject(int s)
        {
            if (s == Academics.Sword || s == Academics.Dungeon) return Body;
            return Academics.IsPractical(s) ? Magic : Mind;
        }

        /// <summary>
        /// 最初の能力値（体力・魅力・器用さ・魔力・知力）。性格と種族から決めた試作用の値。
        /// </summary>
        public static float[] Initial(string letter)
        {
            switch (letter)
            {
                case "A": return new float[] { 70, 55, 45, 80, 65 }; // ヴィエッタ：竜人。魔力が強い
                case "B": return new float[] { 35, 65, 60, 75, 70 }; // シルヴィエル：エルフ。体は弱め
                case "C": return new float[] { 85, 60, 35, 45, 30 }; // ルピナ：銀狼。体力自慢
                case "D": return new float[] { 45, 60, 70, 55, 55 }; // ミレイユ：羊。編み物で器用
                case "E": return new float[] { 55, 75, 40, 70, 55 }; // ラフィーナ：天使族。華がある
                case "F": return new float[] { 45, 50, 55, 5, 55 };  // 篤：魔力なし。元社会人で知力・器用さは少し高め
                case "G": return new float[] { 60, 35, 85, 50, 75 }; // ロッテ：ドワーフ。器用さと知力
                case "H": return new float[] { 55, 70, 40, 50, 35 }; // ニナ：うさぎ。愛嬌
                case "I": return new float[] { 40, 70, 45, 60, 65 }; // リア：王女。気品と教養
                case "J": return new float[] { 60, 55, 70, 45, 50 }; // キッカ：リス。すばしこく手先が器用
                case "K": return new float[] { 35, 65, 45, 75, 55 }; // セレネ：月の精霊。体は弱め、魔力が高い
                default: return new float[] { 50, 50, 50, 50, 50 };
            }
        }

        /// <summary>最初の性技（仮の値。篤は元社会人で少し経験がある）。</summary>
        public static float InitialSex(string letter)
        {
            switch (letter)
            {
                case "F": return 15f;
                case "H": return 10f;  // ニナ：甘えん坊
                case "C": return 8f;   // ルピナ
                case "E": return 6f;   // ラフィーナ
                case "J": return 6f;   // キッカ
                case "A": return 5f;
                case "D": return 5f;
                case "K": return 5f;
                case "G": return 4f;
                case "B": return 3f;
                case "I": return 2f;   // リア：王女で世間知らず
                default: return 5f;
            }
        }
    }
}
