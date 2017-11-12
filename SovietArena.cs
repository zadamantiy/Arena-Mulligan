using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SmartBot.Database;
using SmartBot.Plugins.API;

namespace SmartBot.Mulligan
{
    public static class Extension2
    {
        public static bool ContainsAll<T1>(this IList<T1> list, params T1[] items)
        {
            return !items.Except(list).Any();
        }
    }
    [Serializable]

    public class Paladin : MulliganProfile
    {
        private const bool BlnDebugMode = true;

        private const string Version = "1.0";
        private const string SBuild = "0017b";
        private const string SName = "Universal";

        private const string SDivider = "======================================================";

        private string _log = "";

        private List<Card.Cards> _choices;
        private List<Card.Cards> _keep = new List<Card.Cards>();
        private List<Card.Cards> _myDeck;
        private Card.CClass _opponentClass;

        private readonly IniManager _iniTierList = new IniManager(Directory.GetCurrentDirectory() + @"\MulliganProfiles\Files\SovietArena_TierList.ini");

        private string MulliganInfo()
        {
            string sTierListVersion = _iniTierList.GetString("info", "version", "unknown");
            string sTierListName = _iniTierList.GetString("info", "name", "Basic");

            string info = "\r\n" + SDivider + "\r\nSoviet " + SName + ":" + sTierListName + "\r\n" + SDivider;

            info += "\r\nCore version: Build" + SBuild;
            info += "\r\nTierList version: Build" + sTierListVersion;
            info += "\r\n" + SDivider;

            return info;
        }

        private readonly Dictionary<string, int> _comboDic = new Dictionary<string, int>()
        {
            //coin
            {"10210", 75 },
            {"10211", 100},
            {"11010", 25 },
            {"11020", 75 },
            {"11021", 100},
            {"11110", 75 },
            {"11111", 100},
            {"12110", 90 },
            {"12200", 75 },
            {"12100", 35 },
            {"11100", 25 },
            {"10110", 0  },
            {"10010", 0  },
            {"10020", 0  },
            //no coin
            {"01110", 75 },
            {"01100", 25 },
            {"00110", 0  },
            {"00010", 0  },
            {"00020", -50},
            {"00030", -90},
        };

        //KEEP_CARD
        private bool Keep(string reason, params Card.Cards[] cards)
        {
            var count = true;
            string str = "> Keep: ";
            foreach (var card in cards)
            {
                if (!_choices.Contains(card)) continue;

                var cardTemp = CardTemplate.LoadFromId(card);
                str += cardTemp.Name + ",";
                _choices.Remove(card);
                _keep.Add(card);
                //cardCost[cardTemp.Cost]--;
                count = false;
            }
            if (count) return false;
            str = str.Remove(str.Length - 1);
            if (reason != null)
                str += " because " + reason;
            AddLog(str);

            return true;
        }

        //REMOVE_CARD
        private bool RemoveCard(params Card.Cards[] cards)
        {
            var count = true;
            string str = "> Remove: ";

            foreach (var card in cards)
            {
                if (_keep.Contains(card))
                {
                    //var cardTemp = CardTemplate.LoadFromId(card);
                    str += CardTemplate.LoadFromId(card).Name + ",";
                    _keep.Remove(card);
                    _choices.Add(card);
                    //cardCost[cardTemp.Cost - 1]--;
                    count = false;
                }
            }

            if (count) return false;
            str = str.Remove(str.Length - 1);
            AddLog(str);

            return true;
        }

        //ADD_LOG
        private void AddLog(string s)
        {
            _log += "\r\n" + s;
        }

        //PRINT_LOG
        private void PrintLog()
        {
            Bot.Log(_log);

            string sDir = Directory.GetCurrentDirectory() + @"\Logs\Soviet\Universal\";

            if (!Directory.Exists(sDir))
                Directory.CreateDirectory(sDir);

            File.AppendAllText(Directory.GetCurrentDirectory() + @"\Logs\Soviet\Universal\Universal.log", _log);

            _log = "\r\n---Soviet " + SName + " v" + Version + "---";
        }

        private void LogDebug()
        {
            AddLog("[SU-DЕBUG] Time = " + DateTime.Now);
            AddLog("[SU-DЕBUG] Dir  = " + Directory.GetCurrentDirectory());
            AddLog(SDivider);
        }




        //MAIN

        //TODO: 
        //Fields Has_0Drop, Has_1Drop

        //DECK INFO
        //Fields Deck_Has_No_2Drop, Deck_Has_No_3Drop
        //Field no_rptd_cards

        //Race fields: Beast, murloc etc

        public List<Card.Cards> HandleMulligan(List<Card.Cards> choices, Card.CClass opponentClass, Card.CClass ownClass)
        {
            try
            {
                _myDeck = Bot.CurrentDeck().Cards.Select(card => (Card.Cards)Enum.Parse(typeof(Card.Cards), card)).ToList();
            }
            catch
            {
                _myDeck = new List<Card.Cards> { Cards.AzureDrake, Cards.SirFinleyMrrgglton };
            }

            AddLog(MulliganInfo());

            //Define our globals
            _choices = choices;
            _opponentClass = opponentClass;

            AddLog("Match info: ");
            AddLog("Class: " + ownClass);
            AddLog("Opponent: " + _opponentClass);

            //Defines the coin.
            bool coin = _choices.Count >= 4;
            if (coin)
                Keep(null, Card.Cards.GAME_005);

            AddLog("Coin: " + coin);
            AddLog(SDivider);

            AddLog("Offered:");
            foreach (var card in _choices)
            {
                var cardTmp = CardTemplate.LoadFromId(card);
                AddLog("> " + cardTmp.Name);
            }
            AddLog(SDivider);

            //LOAD ID 

            int[] iCardPts = new int[_choices.Count];
            int[,] iCardInteractionPts = new int[_choices.Count, _choices.Count];


            for (var i = 0; i < choices.Count; i++)
            {
                var card = choices[i];

                int.TryParse(_iniTierList.GetString(String.Format("card_{0}", card), "class_ANY", "-50"), out iCardPts[i]);

                int temp;

                int.TryParse(_iniTierList.GetString(String.Format("card_{0}", card), String.Format("class_{0}", ownClass), "-1917"), out temp);

                iCardPts[i] = (temp == -1917) ? iCardPts[i] : temp;

                int.TryParse(_iniTierList.GetString(String.Format("card_{0}", card), String.Format("opp_{0}", _opponentClass), "0"), out temp);
                iCardPts[i] += temp;

                if (coin)
                {
                    int.TryParse(_iniTierList.GetString(String.Format("card_{0}", card), "coin", "0"), out temp);
                    iCardPts[i] += temp;
                }

                for (var j = 0; j < choices.Count; j++)
                {
                    if (j == i) continue;
                    var card2 = choices[j];
                    int.TryParse(_iniTierList.GetString(String.Format("card_{0}", card), String.Format("pCard_{0}", card2), "0"), out iCardInteractionPts[i, j]);
                }
            }

            int[, , ,] combinationPts = new int[2, 2, 2, 2];
            int[, , ,] combinationPtsPerCrd = new int[2, 2, 2, 2];
            bool[, , ,] combinationCmb = new bool[2, 2, 2, 2];
            int[, , , ,] combinationInt = new int[2, 2, 2, 2, 4];
            int[, , , ,] combinationCst = new int[2, 2, 2, 2, 6];

            string bestComboVal = "0000";
            int bestComboPts = 0;
            int bestComboPtsPerCard = 0;
            int bestCardCount = 0;

            for (var i = 0; i < 2; i++)
            for (var j = 0; j < 2; j++)
            for (var k = 0; k < 2; k++)
            for (var l = 0; (l < 2 && coin || l < 1 && !coin); l++)
            {
                for (var it = 0; it < choices.Count; it++)
                {
                    //var card = choices[it];

                    if ((it == 0 && i == 0) ||
                        (it == 1 && j == 0) ||
                        (it == 2 && k == 0) ||
                        (it == 3 && l == 0)) continue;

                    combinationPts[i, j, k, l] += iCardPts[it];
                    combinationInt[i, j, k, l, it] += iCardPts[it];

                    for (var jt = 0; jt < choices.Count; jt++)
                    {
                        if ((jt == it) ||
                            (jt == 0 && i == 0) ||
                            (jt == 1 && j == 0) ||
                            (jt == 2 && k == 0) ||
                            (jt == 3 && l == 0)) continue;

                        //var card2 = choices[jt];

                        combinationInt[i, j, k, l, jt] += iCardInteractionPts[it, jt];
                        combinationPts[i, j, k, l] += iCardInteractionPts[it, jt];
                    }
                }

                for (var it = 0; it < choices.Count; it++)
                {
                    var card = choices[it];
                    var cardTmp = CardTemplate.LoadFromId(card);

                    if ((it == 0 && i == 0) ||
                        (it == 1 && j == 0) ||
                        (it == 2 && k == 0) ||
                        (it == 3 && l == 0)) continue;

                    int tmp = cardTmp.Cost;
                    /* FIXME: dirty hack */
                    if (card == Cards.NerubianProphet) tmp = 3;

                    combinationCst[i, j, k, l, Math.Min(5, tmp)]++;
                }

                //int ptsNcPerCard = combinationPts[i, j, k, l] / (i+j+k+l+1);

                //TODO: MB BETTER TO REWORK COMBO
                var curCombo = String.Format("{0}{1}{2}{3}{4}",
                    coin ? 1 : 0,                  //0
                    combinationCst[i, j, k, l, 1], //1
                    combinationCst[i, j, k, l, 2], //2
                    combinationCst[i, j, k, l, 3], //3
                    combinationCst[i, j, k, l, 4]); //4

                if (_comboDic.ContainsKey(curCombo) && combinationCst[i, j, k, l, 5] == 0)
                {
                    combinationPts[i, j, k, l] += _comboDic[curCombo];
                    combinationCmb[i, j, k, l] = true;
                }


                if (!combinationCmb[i, j, k, l])
                {
                    combinationPts[i, j, k, l] -= combinationCst[i, j, k, l, 5] * 150;
                    combinationPts[i, j, k, l] -= combinationCst[i, j, k, l, 4] * 100;
                    combinationPts[i, j, k, l] -= combinationCst[i, j, k, l, 3] * 75;
                    if (!coin && combinationCst[i, j, k, l, 2] > 1)
                        combinationPts[i, j, k, l] -= (combinationCst[i, j, k, l, 2] - 1) * 55;
                    else if (coin && combinationCst[i, j, k, l, 2] > 2)
                    {
                        combinationPts[i, j, k, l] -= (combinationCst[i, j, k, l, 2] - 2) * 55;
                    }
                    if (!coin && combinationCst[i, j, k, l, 1] > 2)
                        combinationPts[i, j, k, l] -= (combinationCst[i, j, k, l, 1] - 2) * 30;
                    else if (coin && combinationCst[i, j, k, l, 1] > 2)
                    {
                        combinationPts[i, j, k, l] -= (combinationCst[i, j, k, l, 2] - 1) * 20;
                    }
                }

                var cardCount = i + j + k + l;
                if (i + j + k + l != 0)
                    combinationPtsPerCrd[i, j, k, l] = combinationPts[i, j, k, l] / cardCount;

                if (BlnDebugMode)
                {
                    AddLog(String.Format("Combination: {0}{1}{2}{3} -- Points: {4,5} -- Pts/Card: {5}", i, j, k, l, combinationPts[i, j, k, l], combinationPtsPerCrd[i, j, k, l]));
                }

                if (combinationPts[i, j, k, l] >= cardCount * 100 && (bestComboPts < combinationPts[i, j, k, l] || bestComboPts == combinationPts[i, j, k, l] && cardCount > bestCardCount))
                {
                    bestCardCount = cardCount;
                    bestComboPts = combinationPts[i, j, k, l];
                    bestComboPtsPerCard = combinationPtsPerCrd[i, j, k, l];
                    bestComboVal = String.Format("{0}{1}{2}{3}", i, j, k, l);
                }
            }

            AddLog(String.Format("Best Combination: {0} -- Points: {1} -- Pts/Card: {2}", bestComboVal, bestComboPts, bestComboPtsPerCard));
            AddLog(SDivider);

            AddLog("Finally keeping:");

            List<Card.Cards> lccKeeping = new List<Card.Cards>();
            for (var i = _choices.Count - 1; i >= 0; i--)
            {
                if (bestComboVal[i] == '1')
                    lccKeeping.Add(_choices[i]);
            }

            foreach (var card in lccKeeping)
            {
                Keep(null, card);
            }

            if (lccKeeping.Count == 0) AddLog("Nothing");

            AddLog(SDivider);

            if (BlnDebugMode)
                LogDebug();

            //Ending
            PrintLog();

            return _keep;
        }
    }

    //TODO: Card class
    //public class 
    //{
    //
    //}

    public class IniManager
    {
        private const int CSize = 1024;

        public IniManager(string path)
        {
            Path = path;
        }

        //For empty
        public IniManager()
            : this("")
        {
        }

        public string Path { get; set; }

        public string GetString(string section, string key, string Default = null)
        {
            StringBuilder buffer = new StringBuilder(CSize);
            GetString(section, key, Default, buffer, CSize, Path);
            return buffer.ToString();
        }

        public void WriteString(string section, string key, string sValue)
        {
            WriteString(section, key, sValue, Path);
        }

        [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileString")]
        private static extern int GetString(string section, string key, string def, StringBuilder bufer, int size,
            string path);

        [DllImport("kernel32.dll", EntryPoint = "WritePrivateProfileString")]
        private static extern int WriteString(string section, string key, string str, string path);
    }
}