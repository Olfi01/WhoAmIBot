﻿using FlomBotFactory;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Telegram.Bot.Types;
using UpdateEventArgs = Telegram.Bot.Args.UpdateEventArgs;
using File = System.IO.File;
using Telegram.Bot.Types.Enums;
using WhoAmIBotSpace.Classes;
using Game = WhoAmIBotSpace.Classes.Game;
using User = WhoAmIBotSpace.Classes.User;
using Group = WhoAmIBotSpace.Classes.Group;
using WhoAmIBotSpace.Helpers;
using Newtonsoft.Json;
using System.Threading;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Telegram.Bot.Types.InlineKeyboardButtons;

namespace WhoAmIBotSpace
{
    public class WhoAmIBot : FlomBot
    {
        #region Properties
        public override string Name => "Who am I bot";
        public string Username { get; set; }
        #endregion
        #region Constants
        private const string baseFilePath = "C:\\Olfi01\\WhoAmIBot\\";
        private const string sqliteFilePath = baseFilePath + "db.sqlite";
        private const string connectionString = "Data Source=\"" + sqliteFilePath + "\";";
        private const long testingGroupId = -1001070844778;
        private static readonly List<string> gameQueries = new List<string>()
        {
            "yes@",
            "idk@",
            "no@",
            "guess@",
            "giveup@"
        };
        protected new static readonly Flom Flom = new Flom();
        #endregion
        #region Fields
        private SQLiteConnection sqliteConn;
        private List<Node> Nodes = new List<Node>();
        #endregion

        #region Constructors and FlomBot stuff
        public WhoAmIBot(string token) : base(token)
        {
            if (!Directory.Exists(baseFilePath)) Directory.CreateDirectory(baseFilePath);
            if (!File.Exists(sqliteFilePath)) SQLiteConnection.CreateFile(sqliteFilePath);
            InitSqliteConn();
            ReadCommands();
        }

        private void InitSqliteConn()
        {
            sqliteConn = new SQLiteConnection(connectionString);
            sqliteConn.Open();
        }

        public override bool StartBot()
        {
            try
            {
                var task = client.GetMeAsync();
                task.Wait();
                Username = task.Result.Username;
                client.OnReceiveError += Client_OnReceiveError;
                client.OnReceiveGeneralError += Client_OnReceiveError;
                client.OnCallbackQuery += Client_OnCallbackQuery;
            }
            catch
            {
                return false;
            }
            return base.StartBot();
        }

        public override bool StopBot()
        {
            foreach (var node in Nodes)
            {
                node.Stop();
            }
            client.OnReceiveError -= Client_OnReceiveError;
            client.OnReceiveGeneralError -= Client_OnReceiveError;
            client.OnCallbackQuery -= Client_OnCallbackQuery;
            return base.StopBot();
        }
        #endregion
        #region On Update
        protected override void Client_OnUpdate(object sender, UpdateEventArgs e)
        {
            
        }
        #endregion
        #region On callback query
        private void Client_OnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            if (!e.CallbackQuery.Data.Contains("@")
                || !long.TryParse(e.CallbackQuery.Data.Substring(e.CallbackQuery.Data.IndexOf("@") + 1), out long id)
                || e.CallbackQuery.Message == null) return;
            var q = ExecuteSql("SELECT * FROM Games");
            if (q.Exists(x => x.Count > 0 && x[0] == id.ToString())) return;
            if (!gameQueries.Exists(x => e.CallbackQuery.Data.StartsWith(x))) return;
            else
            {
                Message cmsg = e.CallbackQuery.Message;
                var query = ExecuteSql("SELECT seq FROM sqlite_sequence WHERE name = 'Games'");
                long last = 0;
                if (query.Count < 1) last = 0;
                else
                {
                    if (query[0].Count < 1) last = 0;
                    else if (!long.TryParse(query[0][0], out last)) last = 0;
                }
                if (id < last + 1)
                {
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.GameNoLongerRunning, e.CallbackQuery.From.Id));
                    client.EditMessageReplyMarkupAsync(cmsg.Chat.Id, cmsg.MessageId, null);
                }
            }
        }
        #endregion

        #region Settings
        #region Cancelgame
        private void SetCancelgame(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if ((!d.StartsWith("cancelgameYes@") && !d.StartsWith("cancelgameNo@"))
                || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid || !Groups.Exists(x => x.Id == groupid)
                || e.CallbackQuery.Message == null
                || e.CallbackQuery.From.Id != chat) return;
                var par = new Dictionary<string, object>() { { "id", groupid } };
                switch (d.Remove(d.IndexOf("@")))
                {
                    case "cancelgameYes":
                        Groups.Find(x => x.Id == groupid).CancelgameAdmin = true;
                        par.Add("b", true);
                        ExecuteSql("UPDATE Groups SET CancelgameAdmin=@b WHERE Id=@id", par);
                        EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId, 
                            Strings.CancelgameA, null, GetString(Strings.True, groupid));
                        break;
                    case "cancelgameNo":
                        Groups.Find(x => x.Id == groupid).CancelgameAdmin = false;
                        par.Add("b", false);
                        ExecuteSql("UPDATE Groups SET CancelgameAdmin=@b WHERE Id=@id", par);
                        EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId, 
                            Strings.CancelgameA, null, GetString(Strings.False, groupid));
                        break;
                }
                mre.Set();
            };
            SendLangMessage(chat, Strings.CancelgameQ, ReplyMarkupMaker.InlineYesNo(GetString(Strings.Yes, groupid), $"cancelgameYes@{groupid}",
                GetString(Strings.No, groupid), $"cancelgameNo@{groupid}"), 
                GetString(Groups.Find(x => x.Id == groupid).CancelgameAdmin? Strings.True : Strings.False, groupid));
            try
            {
                client.OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                client.OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region Join Timeout
        private void SetJoinTimeout(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if (!d.StartsWith("joinTimeout:") || !d.Contains("@") || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid || e.CallbackQuery.From.Id != chat
                || e.CallbackQuery.Message == null
                || !int.TryParse(d.Remove(d.IndexOf("@")).Substring(d.IndexOf(":") + 1), out int val)
                || !Groups.Exists(x => x.Id == groupid)) return;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                Groups.Find(x => x.Id == groupid).JoinTimeout = val;
                var par = new Dictionary<string, object>()
                {
                    { "val", val },
                    { "id", groupid }
                };
                ExecuteSql("UPDATE Groups SET JoinTimeout=@val WHERE id=@id", par);
                EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId, 
                    Strings.JoinTimeoutA, null, val.ToString());
            };
            var row = new InlineKeyboardButton[3];
            row[0] = new InlineKeyboardCallbackButton("2", $"joinTimeout:2@{groupid}");
            row[1] = new InlineKeyboardCallbackButton("5", $"joinTimeout:5@{groupid}");
            row[2] = new InlineKeyboardCallbackButton("10", $"joinTimeout:10@{groupid}");
            InlineKeyboardMarkup markup = new InlineKeyboardMarkup(row);
            SendLangMessage(chat, groupid, Strings.JoinTimeoutQ, markup,
                maxIdleJoinTime.TotalMinutes.ToString(), Groups.Find(x => x.Id == groupid).JoinTimeout.ToString());
            try
            {
                client.OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                client.OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region Game Timeout
        private void SetGameTimeout(long groupid, long chat)
        {
            var mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if (!d.StartsWith("gameTimeout:") || !d.Contains("@") || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long grp)
                || grp != groupid || e.CallbackQuery.From.Id != chat
                || e.CallbackQuery.Message == null
                || !int.TryParse(d.Remove(d.IndexOf("@")).Substring(d.IndexOf(":") + 1), out int val)
                || !Groups.Exists(x => x.Id == groupid)) return;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                Groups.Find(x => x.Id == groupid).GameTimeout = val;
                var par = new Dictionary<string, object>()
                {
                    { "val", val },
                    { "id", groupid }
                };
                ExecuteSql("UPDATE Groups SET GameTimeout=@val WHERE id=@id", par);
                EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId, 
                    Strings.GameTimeoutA, null, $"{val / 60}h");
            };
            var row = new InlineKeyboardButton[4];
            row[0] = new InlineKeyboardCallbackButton("1h", $"gameTimeout:60@{groupid}");
            row[1] = new InlineKeyboardCallbackButton("6h", $"gameTimeout:360@{groupid}");
            row[2] = new InlineKeyboardCallbackButton("12h", $"gameTimeout:720@{groupid}");
            row[3] = new InlineKeyboardCallbackButton("24h", $"gameTimeout:1440@{groupid}");
            InlineKeyboardMarkup markup = new InlineKeyboardMarkup(row);
            SendLangMessage(chat, groupid, Strings.GameTimeoutQ, markup,
                $"{maxIdleGameTime.TotalHours}h", $"{Groups.Find(x => x.Id == groupid).GameTimeout / 60}h");
            try
            {
                client.OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                client.OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #endregion
        #region Helpers
        private Group GetGroup(Game g)
        {
            return Groups.Find(x => x.Id == g.GroupId);
        }
        #endregion

        #region Language
        #region Get string
        private string GetString(string key, string langCode)
        {
            var par = new Dictionary<string, object>() { { "key", key } };
            var q = ExecuteSql($"SELECT value FROM '{langCode}' WHERE key=@key", par);
            string query = "";
            if (q.Count < 1 || q[0].Count < 1 || q[0][0].StartsWith("SQL logic error or missing database"))
            {
                q = ExecuteSql($"SELECT value FROM '{defaultLangCode}' WHERE key=@key", par);
                if (q.Count < 1 || q[0].Count < 1)
                {
                    query = $"String {key} missing. Inform @Olfi01.";
                }
                else
                {
                    query = q[0][0];
                }
            }
            else
            {
                query = q[0][0];
            }
            return query;
        }
        
        private string GetString(string key, long langFrom)
        {
            return GetString(key, LangCode(langFrom));
        }
        #endregion
        #region Lang code
        private string LangCode(long id)
        {
            string key = "";
            if (Groups.Exists(x => x.Id == id)) key = Groups.Find(x => x.Id == id).LangKey;
            else if (Users.Exists(x => x.Id == id)) key = Users.Find(x => x.Id == id).LangKey;
            else key = defaultLangCode;
            var par = new Dictionary<string, object>() { { "key", key } };
            var q = ExecuteSql("SELECT key FROM ExistingLanguages WHERE Key=@key", par);
            if (q.Count > 0 && q[0].Count > 0) return q[0][0];
            else
            {
                q = ExecuteSql("SELECT key FROM existinglanguages WHERE key LIKE (SUBSTR((SELECT langkey FROM users LIMIT 1), 0, 3)||'-%')");
                if (q.Count > 0 && q[0].Count > 0) return q[0][0];
                else return defaultLangCode;
            }
        }
        #endregion
        #region Send Lang Message
        private bool SendLangMessage(long chatid, string key, IReplyMarkup markup = null)
        {
            try
            {
                var task = client.SendTextMessageAsync(chatid, GetString(key, LangCode(chatid)),
                    replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SendLangMessage(long chatid, string key, IReplyMarkup markup, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(chatid));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SendLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup = null)
        {
            try
            {
                var task = client.SendTextMessageAsync(chatid, GetString(key, LangCode(langFrom)),
                    replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SendLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(langFrom));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SendAndGetLangMessage(long chatid, long langFrom, string key, IReplyMarkup markup,
            out Message message, out string text, params string[] par)
        {
            try
            {
                string toSend = GetString(key, LangCode(langFrom));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.SendTextMessageAsync(chatid, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                message = task.Result;
                text = toSend;
                return true;
            }
            catch
            {
                message = null;
                text = null;
                return false;
            }
        }
        #endregion
        #region Edit Lang Message
        private bool EditLangMessage(long chatid, long langFrom, int messageId, string key,
            IReplyMarkup markup, string appendStart, out Message sent, out string text, params string[] par)
        {
            try
            {
                string toSend = appendStart + GetString(key, LangCode(langFrom));
                for (int i = 0; i < par.Length; i++)
                {
                    toSend = toSend.Replace("{" + i + "}", par[i]);
                }
                var task = client.EditMessageTextAsync(chatid, messageId, toSend, replyMarkup: markup, parseMode: ParseMode.Html);
                task.Wait();
                sent = task.Result;
                text = toSend;
                return true;
            }
            catch
            {
                sent = null;
                text = null;
                return false;
            }
        }

        private bool EditLangMessage(long chatid, long langFrom, int messageId, string key, IReplyMarkup markup = null)
        {
            return EditLangMessage(chatid, langFrom, messageId, key, markup, "", out var u, out var u2);
        }

        private bool EditLangMessage(long chatid, long langFrom, int messageId, string key, IReplyMarkup markup, params string[] par)
        {
            return EditLangMessage(chatid, langFrom, messageId, key, markup, "", out var u, out var u2, par);
        }
        #endregion
        #region Get Lang File
#if DEBUG
        public LangFile GetLangFile(string key, bool completify = true)
#else
        private LangFile GetLangFile(string key, bool completify = true)
#endif
        {
            var par = new Dictionary<string, object>()
            {
                { "key", key }
            };
            var query = ExecuteSql("SELECT Name FROM ExistingLanguages WHERE Key=@key", par);
            LangFile lf = new LangFile()
            {
                LangKey = key,
                Name = query[0][0],
                Strings = new List<JString>()
            };
            string command = $"SELECT Key, Value FROM '{key}'";
            if (completify) command = $"SELECT key, value FROM '{key}' UNION ALL" +
                    $" SELECT key, value FROM '{defaultLangCode}' WHERE key NOT IN (SELECT key FROM '{key}')";
            query = ExecuteSql(command);
            foreach (var row in query)
            {
                if (row.Count < 2) continue;
                lf.Strings.Add(new JString(row[0], row[1]));
            }
            return lf;
        }
        #endregion
        #endregion
        #region Command Methods
        #region Read Commands
        private void ReadCommands()
        {
            commands.Add("/sql", new Action<Message>(SQL_Command));
            commands.Add("/startgame", new Action<Message>(Startgame_Command));
            commands.Add("/start", new Action<Message>(Start_Command));
            commands.Add("/join", new Action<Message>(Join_Command));
            commands.Add("/cancelgame", new Action<Message>(Cancelgame_Command));
            commands.Add("/go", new Action<Message>(Go_Command));
            commands.Add("/setlang", new Action<Message>(Setlang_Command));
            commands.Add("/setdb", new Action<Message>(Setdb_Command));
            commands.Add("/nextgame", new Action<Message>(Nextgame_Command));
            commands.Add("/stats", new Action<Message>(Stats_Command));
            commands.Add("/getlang", new Action<Message>(Getlang_Command));
            commands.Add("/uploadlang", new Action<Message>(Uploadlang_Command));
            commands.Add("/maint", new Action<Message>(Maint_Command));
            commands.Add("/help", new Action<Message>(Help_Command));
            commands.Add("/getgames", new Action<Message>(Getgames_Command));
            commands.Add("/getroles", new Action<Message>(Getroles_Command));
            commands.Add("/communicate", new Action<Message>(Communicate_Command));
            commands.Add("/giveup", new Action<Message>(Giveup_Command));
            commands.Add("/langinfo", new Action<Message>(Langinfo_Command));
            commands.Add("/backup", new Action<Message>(Backup_Command));
            commands.Add("/rate", new Action<Message>(Rate_Command));
            commands.Add("/canceljoin", new Action<Message>(Canceljoin_Command));
            commands.Add("/identify", new Action<Message>(Identify_Command));
            commands.Add("/ping", new Action<Message>(Ping_Command));
            commands.Add("/settings", new Action<Message>(Settings_Command));
        }
        #endregion

        #region /backup
        private void Backup_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
                return;
            }
            const string temp = "temp.sqlite";
            if (File.Exists(temp)) File.Delete(temp);
            File.Copy(sqliteFilePath, temp);
            client.SendDocumentAsync(msg.Chat.Id, new FileToSend("db.sqlite", File.OpenRead(temp)), caption: "#whoamibotbackup");
        }
        #endregion
        #region /cancelgame
        private void Cancelgame_Command(Message msg)
        {
            if (msg.Text.Contains(" ") && GlobalAdmins.Contains(msg.From.Id))
            {
                if (long.TryParse(msg.Text.Substring(msg.Text.IndexOf(" ")), out long id))
                {
                    var g2 = GamesRunning.Find(x => x.GroupId == id || x.Id == id);
                    if (g2 != null)
                    {
                        CancelGame(g2);
                        SendLangMessage(msg.Chat.Id, Strings.GameCancelled);
                        SendLangMessage(g2.GroupId, Strings.GameCancelledByGlobalAdmin);
                        return;
                    }
                }
            }
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            if (!Groups.Exists(x => x.Id == msg.Chat.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.Chat.Id }, { "langCode", defaultLangCode },
                    { "name", msg.Chat.Title } };
                ExecuteSql("INSERT INTO Groups (Id, LangKey, Name) VALUES(@id, @langCode, @name)", par);
                Groups.Add(new Group(msg.Chat.Id) { Name = msg.Chat.Title });
            }
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                bool cancancel = !Groups.Find(x => x.Id == msg.Chat.Id).CancelgameAdmin;
                if (msg.Chat.Type.IsGroup())
                {
                    var t = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                    t.Wait();
                    if (t.Result.Status == ChatMemberStatus.Administrator || t.Result.Status == ChatMemberStatus.Creator) cancancel = true;
                }
                if (!cancancel)
                {
                    SendLangMessage(msg.Chat.Id,  Strings.AdminOnly);
                    return;
                }
            }
            if (!Help.Longer(g.Players, g.TotalPlayers).Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            CancelGame(g);
            SendLangMessage(msg.Chat.Id, Strings.GameCancelled);
        }
        #endregion
        #region /canceljoin
        private void Canceljoin_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
                return;
            }
            foreach (var g in GamesRunning.FindAll(x => x.State == GameState.Joining))
            {
                CancelGame(g);
                client.SendTextMessageAsync(msg.Chat.Id, $"A game was cancelled in {g.GroupName} ({g.GroupId})");
                SendLangMessage(g.GroupId, Strings.GameCancelledByGlobalAdmin);
            }
        }
        #endregion
        #region /communicate
        private void Communicate_Command(Message msg)
        {
            if (msg.Chat.Type != ChatType.Private || !msg.Text.Contains(" ")) return;
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGlobalAdmin);
                return;
            }
            if (!long.TryParse(msg.Text.Substring(msg.Text.IndexOf(" ")), out long linked)) return;
            else
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                EventHandler<MessageEventArgs> mHandler = (sender, e) =>
                {
                    var msg2 = e.Message;
                    var priv = msg.Chat.Id;
                    if (msg2.Chat.Id != linked && msg2.Chat.Id != priv) return;
                    else if (msg2.Chat.Id == linked) client.ForwardMessageAsync(priv, linked, msg2.MessageId);
                    else if (msg2.Chat.Id == priv)
                    {
                        if (msg2.Type == MessageType.TextMessage &&
                        (msg2.Text == "/communicate" || msg2.Text == $"/communicate@{Username}"))
                        {
                            mre.Set();
                            return;
                        }
                        client.ForwardMessageAsync(linked, priv, msg2.MessageId);
                    }
                };
                try
                {
                    client.OnMessage += mHandler;
                    client.SendTextMessageAsync(msg.Chat.Id, "Communication started.");
                    SendLangMessage(linked, Strings.GASpeaking);
                    mre.WaitOne();
                }
                finally
                {
                    client.OnMessage -= mHandler;
                    client.SendTextMessageAsync(msg.Chat.Id, "Communication stopped.");
                    SendLangMessage(linked, Strings.MessageLinkStopped);
                }
            }
        }
        #endregion
        #region /getgames
        private void Getgames_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
                return;
            }
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) => { };
            cHandler = (sender, e) =>
            {
                string data = e.CallbackQuery.Data;
                if ((!data.StartsWith("cancel:") && !data.StartsWith("communicate:") && !data.StartsWith("close@"))
                || !data.Contains("@") || data.IndexOf("@") != data.LastIndexOf("@")
                || !long.TryParse(data.Substring(data.IndexOf("@") + 1), out long chatid)
                || chatid != msg.Chat.Id || e.CallbackQuery.Message == null) return;
                if (!GlobalAdmins.Contains(e.CallbackQuery.From.Id))
                {
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.NoGlobalAdmin, LangCode(msg.Chat.Id)));
                    return;
                }
                if (data.StartsWith("close@"))
                {
                    client.EditMessageReplyMarkupAsync(e.CallbackQuery.Message.Chat.Id, e.CallbackQuery.Message.MessageId, null);
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    client.OnCallbackQuery -= cHandler;
                    return;
                }
                if (!long.TryParse(data.Remove(data.IndexOf("@")).Substring(data.IndexOf(":") + 1), out long groupid)) return;
                var action = data.Remove(data.IndexOf(":"));
                switch (action)
                {
                    case "cancel":
                        if (!GamesRunning.Exists(x => x.GroupId == groupid))
                        {
                            client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, "That game is no longer running.");
                            return;
                        }
                        Game g = GamesRunning.Find(x => x.GroupId == groupid);
                        CancelGame(g);
                        SendLangMessage(g.GroupId, Strings.GameCancelledByGlobalAdmin);
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.GameCancelled, e.CallbackQuery.From.Id));
                        return;
                    case "communicate":
                        Thread t = new Thread(() =>
                        {
                            try
                            {
                                commands["/communicate"].Invoke(new CommDummyMsg(from: e.CallbackQuery.From.Id, groupid: groupid));
                            }
                            catch (Exception ex)
                            {
                                client.SendTextMessageAsync(Flom, $"Who am I bot\n{ex.Message}\n{ex.StackTrace}");
                            }
                        });
                        t.Start();
                        currentThreads.Add(t);
                        return;
                }
            };
            foreach (var s in string.Join("\n\n",
                GamesRunning.Select(x => $"{x.Id} - {x.GroupName} ({x.GroupId}): {x.State} {x.GetPlayerList()}")).Split(2000))
                client.SendTextMessageAsync(msg.Chat.Id, s, replyMarkup: ReplyMarkupMaker.InlineGetGames(GamesRunning, msg.Chat.Id)).Wait();
            client.OnCallbackQuery += cHandler;
        }
        #endregion
        #region /getlang
        private void Getlang_Command(Message msg)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!e.CallbackQuery.Data.StartsWith("lang:") || e.CallbackQuery.From.Id != msg.From.Id) return;
                var split = e.CallbackQuery.Data.Split(':', '@');
                string key = split[1];
                long groupId = Convert.ToInt64(split[2]);
                if (groupId != msg.Chat.Id) return;
                LangFile lf = GetLangFile(key);
                string path = $"{key}.txt";
                File.WriteAllText(path, JsonConvert.SerializeObject(lf, Formatting.Indented), Encoding.UTF8);
                using (var str = File.OpenRead(path))
                {
                    client.SendDocumentAsync(msg.Chat.Id, new FileToSend(path, str), caption: null).Wait();
                }
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.SelectLanguage,
                ReplyMarkupMaker.InlineChooseLanguage(ExecuteSql(allLangSelector), msg.Chat.Id),
                out Message sent, out var u);
            client.OnCallbackQuery += cHandler;
            mre.WaitOne();
            client.OnCallbackQuery -= cHandler;
            EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.OneMoment, null, "", out var u2, out u);

        }
        #endregion
        #region /getroles
        private void Getroles_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.TotalPlayers.Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            var p = g.TotalPlayers.Find(x => x.Id == msg.From.Id);
            if (!g.DictFull())
            {
                SendLangMessage(msg.Chat.Id, Strings.RolesNotSet);
                return;
            }
            string rl = "";
            foreach (var kvp in g.RoleIdDict)
            {
                if (kvp.Key != p.Id) rl += $"\n<b>{WebUtility.HtmlEncode(g.TotalPlayers.Find(x => x.Id == kvp.Key).Name)}:</b> " +
                        $"<i>{WebUtility.HtmlEncode(kvp.Value)}</i>";
            }
            SendLangMessage(msg.From.Id, msg.Chat.Id, Strings.RolesAre, null, rl);
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.SentPM);
        }
        #endregion
        #region /giveup
        private void Giveup_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.Players.Exists(x => x.Id == msg.From.Id && !x.GaveUp))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            var p = g.Players.Find(x => x.Id == msg.From.Id);
            if (g.Turn == p)
            {
                SendLangMessage(msg.Chat.Id, Strings.ItsYourTurnCantGiveUp);
                return;
            }
            if (!g.DictFull())
            {
                SendLangMessage(msg.Chat.Id, Strings.RolesNotSet);
                return;
            }
            p.GaveUp = true;
            SendLangMessage(msg.From.Id, g.GroupId, Strings.YouGaveUp);
            var role = g.RoleIdDict.ContainsKey(msg.From.Id) ? g.RoleIdDict[msg.From.Id] : "failed to find role";
            SendLangMessage(g.GroupId, Strings.GaveUp, null, p.Name, g.RoleIdDict[msg.From.Id]);
        }
        #endregion
        #region /go
        private void Go_Command(Message msg)
        {
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            if (!g.Players.Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInGame);
                return;
            }
            if (g.Players.Count < minPlayerCount && msg.Chat.Id != testingGroupId)
            {
                SendLangMessage(msg.Chat.Id, Strings.NotEnoughPlayers);
                return;
            }
            if (g.State != GameState.Joining)
            {
                SendLangMessage(msg.Chat.Id, Strings.GameRunning);
                return;
            }
            ParameterizedThreadStart pts = new ParameterizedThreadStart(StartGameFlow);
            Thread t = new Thread(pts);
            g.Thread = t;
            g.InactivityTimer.Change(GetGroup(g).GameTimeout * 60 * 1000, Timeout.Infinite);
            t.Start(g);
        }
        #endregion
        #region /help
        private void Help_Command(Message msg)
        {
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Help);
        }
        #endregion
        #region /identify
        private void Identify_Command(Message msg)
        {
            ChatId i = Flom;
            if (i.Identifier != msg.From.Id) return;
            client.SendTextMessageAsync(msg.Chat.Id, "Yep, you are my developer", replyToMessageId: msg.MessageId);
        }
        #endregion
        #region /join
        private void Join_Command(Message msg)
        {
            if (Users.Exists(x => x.Id == msg.From.Id))
            {
                var u = Users.Find(x => x.Id == msg.From.Id);
                if (u.Name != msg.From.FullName() || u.Username != msg.From.Username)
                {
                    var par = new Dictionary<string, object>() { { "id", msg.From.Id },
                    { "name", msg.From.FullName() }, { "username", msg.From.Username } };
                    ExecuteSql("UPDATE Users SET Name=@name, Username=@username WHERE id=@id", par);
                    u.Name = msg.From.FullName();
                    u.Username = msg.From.Username;
                }
            }
            if (!GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGameRunning);
                return;
            }
            Game g = GamesRunning.Find(x => x.GroupId == msg.Chat.Id);
            AddPlayer(g, new Player(msg.From.Id, msg.From.FullName()));
            g.InactivityTimer.Change(g.Group.JoinTimeout * 60 * 1000, Timeout.Infinite);
        }
        #endregion
        #region /langinfo
        private void Langinfo_Command(Message msg)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            Message sent = null;
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!SelectLangRegex.IsMatch(e.CallbackQuery.Data)
                || !long.TryParse(e.CallbackQuery.Data.Substring(e.CallbackQuery.Data.IndexOf("@") + 1), out long gId)
                || gId != msg.Chat.Id) return;
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                var data = e.CallbackQuery.Data;
                var atI = data.IndexOf("@");
                var dpI = data.IndexOf(":");
                var key = data.Remove(atI).Substring(dpI + 1);
                var query = ExecuteSql($"SELECT Key, Value FROM '{key}'");
                var query2 = ExecuteSql($"SELECT key, value FROM '{defaultLangCode}'");
                var missing = new List<string>();
                foreach (var row in query2)
                {
                    if (!query.Exists(x => x[0] == row[0])) missing.Add(row[0]);
                }
                foreach (var s in query)
                {
                    if (!query2.Exists(x => x[0] == s[0]))
                    {
                        Console.WriteLine(s[0]);
                    }
                    var enStr = query2.Find(x => x[0] == s[0])[1];
                    int extras = 0;
                    while (true)
                    {
                        string tag = "{" + extras + "}";
                        if (enStr.Contains(tag))
                        {
                            extras++;
                            if (!s[1].Contains(tag))
                            {
                                missing.Add($"{tag} in {s[0]}");
                            }
                        }
                        else break;
                    }
                }
                EditLangMessage(sent.Chat.Id, sent.Chat.Id, sent.MessageId, Strings.LangInfo, null, "",
                    out var u, out var u2, key, "\n" + missing.ToStringList());
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.SelectLanguage,
                ReplyMarkupMaker.InlineChooseLanguage(ExecuteSql(allLangSelector), msg.Chat.Id),
                out sent, out var u1);
            try
            {
                client.OnCallbackQuery += cHandler;
                mre.WaitOne();
            }
            finally
            {
                client.OnCallbackQuery -= cHandler;
            }
        }
        #endregion
        #region /maint
        private void Maint_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.NoGlobalAdmin);
            }
            if (!Maintenance)
            {
                Maintenance = true;
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Maintenance);
                if (GamesRunning.Count > 0)
                {
                    EventHandler<GameFinishedEventArgs> handler = null;
                    handler = (sender, e) =>
                    {
                        if (GamesRunning.Count < 1) { SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.GamesFinished); GameFinished -= handler; }
                    };
                    GameFinished += handler;
                }
                else
                {
                    SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.GamesFinished);
                }
            }
            else
            {
                Maintenance = false;
                SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.MaintenanceOff);
            }
        }
        #endregion
        #region /nextgame
        private void Nextgame_Command(Message msg)
        {
            if (msg.Chat.Type == ChatType.Channel) return;
            if (msg.Chat.Type == ChatType.Private)
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (Nextgame.ContainsKey(msg.Chat.Id) && Nextgame[msg.Chat.Id].Exists(x => x.Id == msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, msg.From.Id,  Strings.AlreadyOnNextgameList);
                return;
            }
            if (!Nextgame.ContainsKey(msg.Chat.Id)) Nextgame.Add(msg.Chat.Id, new List<User>());
            Nextgame[msg.Chat.Id].Add(new User(msg.From.Id));
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.PutOnNextgameList);
        }
        #endregion
        #region /ping
        private void Ping_Command(Message msg)
        {
            var now = DateTime.Now;
            var span = now.Subtract(msg.Date);
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Ping, null, span.TotalSeconds.ToString());
        }
        #endregion
        #region /rate
        private void Rate_Command(Message msg)
        {
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.RateMe, null, "http://t.me/storebot?start={Username}");
        }
        #endregion
        #region /setdb
        private void Setdb_Command(Message msg)
        {
            if (msg.From.Id != Flom || msg.ReplyToMessage == null || msg.ReplyToMessage.Type != MessageType.DocumentMessage) return;
            sqliteConn.Close();
            sqliteConn.Dispose();
            File.Delete(sqliteFilePath);
            using (Stream str = File.OpenWrite(sqliteFilePath))
            {
                var task = client.GetFileAsync(msg.ReplyToMessage.Document.FileId, str);
                task.Wait();
                str.Flush();
                str.Close();
            }
            InitSqliteConn();
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.DatabaseUpdated);
        }
        #endregion
        #region /setlang
        private void Setlang_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id) && msg.Chat.Type.IsGroup())
            {
                var t = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                t.Wait();
                if (t.Result.Status != ChatMemberStatus.Administrator && t.Result.Status != ChatMemberStatus.Creator)
                {
                    SendLangMessage(msg.Chat.Id,  Strings.AdminOnly);
                    return;
                }
            }
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
            {
                if (!e.CallbackQuery.Data.StartsWith("lang:") || e.CallbackQuery.From.Id != msg.From.Id) return;
                var split = e.CallbackQuery.Data.Split(':', '@');
                string key = split[1];
                long groupId = Convert.ToInt64(split[2]);
                if (groupId != msg.Chat.Id) return;
                if (msg.Chat.Type.IsGroup())
                {
                    var task = client.GetChatMemberAsync(msg.Chat.Id, msg.From.Id);
                    task.Wait();
                    if (task.Result.Status != ChatMemberStatus.Administrator && task.Result.Status != ChatMemberStatus.Creator)
                    {
                        SendLangMessage(msg.Chat.Id,  Strings.AdminOnly);
                    }
                }
                switch (msg.Chat.Type)
                {
                    case ChatType.Private:
                        if (!Users.Exists(x => x.Id == e.CallbackQuery.From.Id))
                        {
                            Users.Add(new User(e.CallbackQuery.From.Id) { LangKey = key });
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", e.CallbackQuery.From.Id }
                            };
                            ExecuteSql("INSERT INTO Users VALUES(@id, @key)", par);
                        }
                        else
                        {
                            Users.Find(x => x.Id == e.CallbackQuery.From.Id).LangKey = key;
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", e.CallbackQuery.From.Id }
                            };
                            ExecuteSql("UPDATE Users SET LangKey=@key WHERE Id=@id", par);
                        }
                        break;
                    case ChatType.Group:
                    case ChatType.Supergroup:
                        if (!Groups.Exists(x => x.Id == msg.Chat.Id))
                        {
                            Groups.Add(new Group(msg.From.Id) { LangKey = key, Name = msg.Chat.Title });
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", msg.Chat.Id },
                                { "set", true }
                            };
                            ExecuteSql("INSERT INTO Groups(id, langKey, langSet) VALUES(@id, @key, @set)", par);
                        }
                        else
                        {
                            Groups.Find(x => x.Id == msg.Chat.Id).LangKey = key;
                            var par = new Dictionary<string, object>()
                            {
                                { "key", key },
                                { "id", msg.Chat.Id }
                            };
                            ExecuteSql("UPDATE Groups SET LangKey=@key WHERE Id=@id", par);
                        }
                        break;
                }
                client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                mre.Set();
            };
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.SelectLanguage,
                ReplyMarkupMaker.InlineChooseLanguage(ExecuteSql(allLangSelector), msg.Chat.Id),
                out Message sent, out string useless);
            client.OnCallbackQuery += cHandler;
            mre.WaitOne();
            client.OnCallbackQuery -= cHandler;
            EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.LangSet, null, "", out var u, out var u2);
        }
        #endregion
        #region /settings
        private void Settings_Command(Message msg)
        {
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (!Groups.Exists(x => x.Id == msg.Chat.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.Chat.Id }, { "langCode", defaultLangCode },
                    { "name", msg.Chat.Title } };
                ExecuteSql("INSERT INTO Groups (Id, LangKey, Name) VALUES(@id, @langCode, @name)", par);
                Groups.Add(new Group(msg.Chat.Id) { Name = msg.Chat.Title });
            }
            EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) => { };
            cHandler = (sender, e) =>
            {
                var d = e.CallbackQuery.Data;
                if ((!d.StartsWith("joinTimeout@") && !d.StartsWith("gameTimeout@")
                && !d.StartsWith("cancelgameAdmin@") && !d.StartsWith("closesettings@"))
                || d.IndexOf("@") != d.LastIndexOf("@")
                || !long.TryParse(d.Substring(d.IndexOf("@") + 1), out long groupid)
                || groupid != msg.Chat.Id || e.CallbackQuery.Message == null) return;
                var action = d.Remove(d.IndexOf("@"));
                switch (action)
                {
                    case "joinTimeout":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t1 = new Thread(() => SetJoinTimeout(groupid, msg.From.Id));
                        t1.Start();
                        currentThreads.Add(t1);
                        break;
                    case "gameTimeout":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t2 = new Thread(() => SetGameTimeout(groupid, msg.From.Id));
                        t2.Start();
                        currentThreads.Add(t2);
                        break;
                    case "cancelgameAdmin":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        Thread t3 = new Thread(() => SetCancelgame(groupid, msg.From.Id));
                        t3.Start();
                        currentThreads.Add(t3);
                        break;
                    case "closesettings":
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                        EditLangMessage(e.CallbackQuery.Message.Chat.Id, groupid, e.CallbackQuery.Message.MessageId, Strings.Done, null);
                        client.OnCallbackQuery -= cHandler;
                        break;
                }
            };
            var joinTimeout = GetString(Strings.JoinTimeout, msg.Chat.Id);
            var gameTimeout = GetString(Strings.GameTimeout, msg.Chat.Id);
            var cancelgameAdmin = GetString(Strings.CancelgameAdmin, msg.Chat.Id);
            var close = GetString(Strings.Close, msg.Chat.Id);
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.SentPM);
            SendLangMessage(msg.From.Id, msg.Chat.Id, Strings.Settings, ReplyMarkupMaker.InlineSettings(msg.Chat.Id,
                joinTimeout, gameTimeout, cancelgameAdmin, close));
            client.OnCallbackQuery += cHandler;
        }
        #endregion
        #region /start
        private void Start_Command(Message msg)
        {
            if (msg.Chat.Type != ChatType.Private) return;
            if (!Users.Exists(x => x.Id == msg.From.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.From.Id }, { "langcode", msg.From.LanguageCode },
                    { "name", msg.From.FullName() }, { "username", msg.From.Username } };
                ExecuteSql("INSERT INTO Users(Id, LangKey, Name, Username) VALUES(@id, @langcode, @name, @username)", par);
                var u = new User(msg.From.Id) { LangKey = msg.From.LanguageCode };
                Users.Add(u);
                if (string.IsNullOrEmpty(u.LangKey))
                {
                    SendLangMessage(msg.Chat.Id, Strings.Welcome);
                    Setlang_Command(msg);
                }
                else
                {
                    SendLangMessage(msg.Chat.Id, Strings.Welcome);
                }
            }
            else
            {
                var u = Users.Find(x => x.Id == msg.From.Id);
                if (u.Name != msg.From.FullName() || u.Username != msg.From.Username)
                {
                    var par = new Dictionary<string, object>() { { "id", msg.From.Id },
                    { "name", msg.From.FullName() }, { "username", msg.From.Username } };
                    ExecuteSql("UPDATE Users SET Name=@name, Username=@username WHERE id=@id", par);
                    u.Name = msg.From.FullName();
                    u.Username = msg.From.Username;
                }
                SendLangMessage(msg.Chat.Id, Strings.Welcome);
            }
        }
        #endregion
        #region /startgame
        private void Startgame_Command(Message msg)
        {
            if (Maintenance && msg.Chat.Id != testingGroupId)
            {
                SendLangMessage(msg.Chat.Id, Strings.BotUnderMaintenance);
                return;
            }
            if (!msg.Chat.Type.IsGroup())
            {
                SendLangMessage(msg.Chat.Id, Strings.NotInPrivate);
                return;
            }
            if (GamesRunning.Exists(x => x.GroupId == msg.Chat.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.GameRunning);
                return;
            }
            if (Users.Exists(x => x.Id == msg.From.Id))
            {
                var u = Users.Find(x => x.Id == msg.From.Id);
                if (u.Name != msg.From.FullName() || u.Username != msg.From.Username)
                {
                    var par = new Dictionary<string, object>() { { "id", msg.From.Id },
                    { "name", msg.From.FullName() }, { "username", msg.From.Username } };
                    ExecuteSql("UPDATE Users SET Name=@name, Username=@username WHERE id=@id", par);
                    u.Name = msg.From.FullName();
                    u.Username = msg.From.Username;
                }
            }
            if (!Groups.Exists(x => x.Id == msg.Chat.Id))
            {
                var par = new Dictionary<string, object>() { { "id", msg.Chat.Id }, { "langCode", defaultLangCode },
                    { "name", msg.Chat.Title } };
                ExecuteSql("INSERT INTO Groups (Id, LangKey, Name) VALUES(@id, @langCode, @name)", par);
                Groups.Add(new Group(msg.Chat.Id) { Name = msg.Chat.Title });
            }
            else
            {
                Group group = Groups.Find(x => x.Id == msg.Chat.Id);
                if (group.Name != msg.Chat.Title)
                {
                    var par = new Dictionary<string, object>() { { "id", msg.Chat.Id }, { "name", msg.Chat.Title } };
                    ExecuteSql("UPDATE Groups SET Name=@name WHERE id=@id", par);
                    group.Name = msg.Chat.Title;
                }
            }
            var par2 = new Dictionary<string, object>() { { "id", msg.Chat.Id } };
            ExecuteSql($"INSERT INTO Games (groupId) VALUES(@id)", par2);
            string response = ExecuteSql("SELECT id FROM Games WHERE groupId=@id", par2)[0][0];
            Game g = new Game(Convert.ToInt32(response), msg.Chat.Id, msg.Chat.Title, Groups.Find(x => x.Id == msg.Chat.Id));
            GamesRunning.Add(g);
            if (Nextgame.ContainsKey(msg.Chat.Id))
            {
                var toRem = new List<User>();
                foreach (var u in Nextgame[msg.Chat.Id])
                {
                    SendLangMessage(u.Id, Strings.NewGameStarting, null, g.GroupName);
                    toRem.Add(u);
                }
                foreach (var u in toRem)
                {
                    Nextgame[msg.Chat.Id].Remove(u);
                }
            }
            SendLangMessage(msg.Chat.Id, Strings.GameStarted);
            SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.PlayerList, null, out Message m, out var u1, "");
            g.PlayerlistMessage = m;
            AddPlayer(g, new Player(msg.From.Id, msg.From.FullName()));
            Timer t = new Timer(x =>
            {
                CancelGame(g);
                SendLangMessage(g.GroupId, Strings.GameTimedOut);
            }, null, g.Group.JoinTimeout * 60 * 1000, Timeout.Infinite);
            g.InactivityTimer = t;
        }
        #endregion
        #region /stats
        private void Stats_Command(Message msg)
        {
            var par = new Dictionary<string, object>()
            {
                { "id", msg.From.Id }
            };
            string winCount = "";
            var q = ExecuteSql("SELECT COUNT(*) FROM GamesFinished WHERE WinnerId=@id", par);
            if (q.Count < 1 || q[0].Count < 1) winCount = "0";
            else winCount = q[0][0];
            SendLangMessage(msg.Chat.Id, msg.From.Id, Strings.Stats, null, msg.From.FullName(), winCount);
        }
        #endregion
        #region /sql
        private void SQL_Command(Message msg)
        {
            if (!GlobalAdmins.Contains(msg.From.Id)) return;
            try
            {
                string commandText;
                if (msg.ReplyToMessage != null) commandText = msg.ReplyToMessage.Text;
                else commandText = msg.Text.Substring(msg.Entities.Find(x => x.Offset == 0).Length).Trim();
                string response = ExecuteSqlRaw(commandText);
                if (!string.IsNullOrEmpty(response))
                {
                    foreach (var s in response.Split(2000)) client.SendTextMessageAsync(msg.Chat.Id, s, parseMode: ParseMode.Html).Wait();
                }
            }
            catch (Exception e)
            {
                client.SendTextMessageAsync(msg.Chat.Id, $"Exception: {e.Message}\n{e.StackTrace}").Wait();
                while (e.InnerException != null)
                {
                    e = e.InnerException;
                    client.SendTextMessageAsync(msg.Chat.Id, $"Inner Exception: {e.Message}\n{e.StackTrace}").Wait();
                }
            }
        }
        #endregion
        #region /uploadlang
        private void Uploadlang_Command(Message msg)
        {
            if (msg.ReplyToMessage == null || msg.ReplyToMessage.Type != MessageType.DocumentMessage) return;
            if (!GlobalAdmins.Contains(msg.From.Id))
            {
                SendLangMessage(msg.Chat.Id, Strings.NoGlobalAdmin);
                return;
            }
            var now = DateTime.Now;
            var path = $"{now.Hour}-{now.Minute}-{now.Second}-{now.Millisecond}.temp";
            using (var str = File.OpenWrite(path))
            {
                client.GetFileAsync(msg.ReplyToMessage.Document.FileId, str).Wait();
            }
            string text = File.ReadAllText(path);
            LangFile lf = null;
            try
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                lf = JsonConvert.DeserializeObject<LangFile>(text);
                Message sent = null;
                bool permit = false;
                //check if lang exists
                var par = new Dictionary<string, object>()
                {
                    { "key", lf.LangKey },
                    { "name", lf.Name }
                };
                EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
                {
                    if (!GlobalAdmins.Contains(e.CallbackQuery.From.Id)
                    || e.CallbackQuery.Message.MessageId != sent.MessageId
                    || e.CallbackQuery.Message.Chat.Id != sent.Chat.Id) return;
                    if (e.CallbackQuery.Data == "yes") permit = true;
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    mre.Set();
                };
                var query = ExecuteSql("SELECT * FROM ExistingLanguages WHERE Key=@key", par);
                string yes = GetString(Strings.Yes, LangCode(msg.Chat.Id));
                string no = GetString(Strings.No, LangCode(msg.Chat.Id));
                if (query.Count == 0)
                {
                    //create new language
                    var query2 = ExecuteSql($"SELECT key, value FROM '{defaultLangCode}'");
                    var missing = new List<string>();
                    foreach (var row in query2)
                    {
                        if (!lf.Strings.Exists(x => x.Key == row[0])) missing.Add(row[0]);
                    }
                    var toRemove = new List<JString>();
                    foreach (var js in lf.Strings)
                    {
                        if (!query2.Exists(x => x[0] == js.Key))
                        {
                            toRemove.Add(js);
                            continue;
                        }
                        var enStr = query2.Find(x => x[0] == js.Key)[1];
                        int extras = 0;
                        while (true)
                        {
                            string tag = "{" + extras + "}";
                            if (enStr.Contains(tag))
                            {
                                extras++;
                                if (!js.Value.Contains(tag))
                                {
                                    missing.Add($"{tag} in {js.Key}");
                                    if (!toRemove.Exists(x => x.Key == js.Key)) toRemove.Add(js);
                                }
                            }
                            else break;
                        }
                    }
                    foreach (var js in toRemove)
                    {
                        lf.Strings.Remove(js);
                    }
                    SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.CreateLang,
                        ReplyMarkupMaker.InlineYesNo(yes, "yes", no, "no"), out sent, out var u,
                        lf.LangKey, lf.Name, lf.Strings.Count.ToString(), "\n" + missing.ToStringList());
                    try
                    {
                        client.OnCallbackQuery += cHandler;
                        mre.WaitOne();
                    }
                    finally
                    {
                        client.OnCallbackQuery -= cHandler;
                    }
                    if (permit)
                    {
                        ExecuteSql("INSERT INTO ExistingLanguages(Key, Name) VALUES(@key, @name)", par);
                        ExecuteSql($"CREATE TABLE '{lf.LangKey}'(Key varchar primary key, Value varchar)");
                        foreach (var js in lf.Strings)
                        {
                            if (!query2.Exists(x => x[0] == js.Key)) continue;
                            if (missing.Exists(x => x.EndsWith(js.Key))) continue;
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", js.Key },
                                { "value", js.Value }
                            };
                            ExecuteSql($"INSERT INTO '{lf.LangKey}' VALUES(@key, @value)", par1);
                        }
                    }
                }
                else
                {
                    //update old lang
                    query = ExecuteSql($"SELECT Key, Value FROM '{lf.LangKey}'");
                    var query2 = ExecuteSql($"SELECT key, value FROM '{defaultLangCode}'");
                    var missing = new List<string>();
                    int added = 0;
                    int changed = 0;
                    var deleted = new List<string>();
                    foreach (var row in query2)
                    {
                        if (!lf.Strings.Exists(x => x.Key == row[0])) missing.Add(row[0]);
                    }
                    var toRemove = new List<JString>();
                    foreach (var js in lf.Strings)
                    {
                        if (!query2.Exists(x => x[0] == js.Key) && lf.LangKey != defaultLangCode)
                        {
                            toRemove.Add(js);
                            continue;
                        }
                        if (!query.Exists(x => x[0] == js.Key)) added++;
                        else if (query.Find(x => x[0] == js.Key)[1] != js.Value) changed++;
                        var enStr = query2.Exists(x => x[0] == js.Key) ? query2.Find(x => x[0] == js.Key)[1] : "";
                        int extras = 0;
                        while (true)
                        {
                            string tag = "{" + extras + "}";
                            if (enStr.Contains(tag))
                            {
                                extras++;
                                if (!js.Value.Contains(tag))
                                {
                                    missing.Add($"{tag} in {js.Key}");
                                    if (!toRemove.Exists(x => x.Key == js.Key)) toRemove.Add(js);
                                }
                            }
                            else break;
                        }
                    }
                    foreach (var row in query) if (!lf.Strings.Exists(x => x.Key == row[0])) deleted.Add(row[0]);
                    foreach (var js in toRemove) lf.Strings.Remove(js);
                    SendAndGetLangMessage(msg.Chat.Id, msg.Chat.Id, Strings.UpdateLang,
                        ReplyMarkupMaker.InlineYesNo(yes, "yes", no, "no"), out sent, out var u, lf.LangKey, lf.Name,
                        added.ToString(), changed.ToString(), "\n" + deleted.ToStringList(), "\n" + missing.ToStringList());
                    client.OnCallbackQuery += cHandler;
                    mre.WaitOne();
                    client.OnCallbackQuery -= cHandler;
                    if (permit)
                    {
                        foreach (var js in lf.Strings)
                        {
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", js.Key },
                                { "value", js.Value }
                            };
                            if (query.Exists(x => x.Count > 0 && x[0] == js.Key))
                            {
                                ExecuteSql($"UPDATE '{lf.LangKey}' SET Value=@value WHERE Key=@key", par1);
                            }
                            else
                            {
                                ExecuteSql($"INSERT INTO '{lf.LangKey}' VALUES(@key, @value)", par1);
                            }
                        }
                        foreach (var js in toRemove)
                        {
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", js.Key },
                            };
                            ExecuteSql($"DELETE FROM '{lf.LangKey}' WHERE key=@key", par1);
                        }
                        foreach (var key in deleted)
                        {
                            var par1 = new Dictionary<string, object>()
                            {
                                { "key", key },
                            };
                            ExecuteSql($"DELETE FROM '{lf.LangKey}' WHERE key=@key", par1);
                        }
                    }
                }
                if (permit) EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.LangUploaded, null, "", out var u1, out var u2);
                else EditLangMessage(msg.Chat.Id, msg.Chat.Id, sent.MessageId, Strings.UploadCancelled, null, "", out var u1, out var u2);
            }
            catch (Exception x)
            {
                string mess = $"{x.GetType().Name}\n{x.Message}\n";
                if (x.InnerException != null)
                    mess += $"{x.InnerException.Message}";
                SendLangMessage(msg.Chat.Id, Strings.ErrorOcurred, null,
                    mess);
            }
        }
        #endregion
        #endregion

        #region Error handling
        private void Client_OnReceiveError(object sender, EventArgs ea)
        {
            if (ea is ReceiveErrorEventArgs e)
            {
                var ex = e.ApiRequestException;
                client.SendTextMessageAsync(Flom, $"WhoAmIBot: {ex.Message}\n{ex.StackTrace}");
                if (ex.InnerException != null)
                    client.SendTextMessageAsync(Flom, $"WhoAmIBot (inner exception): {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
            }
            else if (ea is ReceiveGeneralErrorEventArgs ge)
            {
                var ex = ge.Exception;
                client.SendTextMessageAsync(Flom, $"WhoAmIBot: {ex.Message}\n{ex.StackTrace}");
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    client.SendTextMessageAsync(Flom, $"WhoAmIBot (inner exception): {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        #endregion

        #region Game Flow
        #region Add player
        private void AddPlayer(Game game, Player player)
        {
            if (game.State != GameState.Joining)
            {
                SendLangMessage(game.GroupId, Strings.GameNotJoining);
                return;
            }
            if (game.Players.Exists(x => x.Id == player.Id))
            {
                SendLangMessage(game.GroupId,  Strings.AlreadyInGame, null, player.Name);
                return;
            }
            if (!SendLangMessage(player.Id, game.GroupId, Strings.JoinedGamePM, null, game.GroupName))
            {
                SendLangMessage(game.GroupId, Strings.PmMe, ReplyMarkupMaker.InlineStartMe(Username), player.Name);
                return;
            }
            game.Players.Add(player);
            if (game.Players.Count > 1) SendLangMessage(game.GroupId, Strings.PlayerJoinedGame, null, player.Name);
            else SendLangMessage(game.GroupId, Strings.PlayerJoinedCantStart, null, player.Name);
            EditLangMessage(game.PlayerlistMessage.Chat.Id, game.GroupId, game.PlayerlistMessage.MessageId,
                Strings.PlayerList, null, "", out var u, out var u1, game.GetPlayerList());
        }
        #endregion
        #region Start game flow
        private void StartGameFlow(object gameObject)
        {
            if (!(gameObject is Game)) return;
            Game game = (Game)gameObject;
            #region Preparation phase
            SendLangMessage(game.GroupId, Strings.GameFlowStarted);
            game.State = GameState.Running;
            game.TotalPlayers = new List<Player>();
            foreach (var p in game.Players)
            {
                game.TotalPlayers.Add(p);
            }
            game.Players.Shuffle();
            for (int i = 0; i < game.Players.Count; i++)
            {
                int next = (i == game.Players.Count - 1) ? 0 : i + 1;
                SendLangMessage(game.Players[i].Id, game.GroupId, Strings.ChooseRoleFor, null, game.Players[next].Name);
            }
            ManualResetEvent mre = new ManualResetEvent(false);
            EventHandler<MessageEventArgs> eHandler = (sender, e) =>
               {
                   if (!game.Players.Exists(x => x.Id == e.Message.From.Id)
                   || e.Message.Type != MessageType.TextMessage || e.Message.Chat.Type != ChatType.Private) return;
                   Player p = game.Players.Find(x => x.Id == e.Message.From.Id);
                   int pIndex = game.Players.IndexOf(p);
                   int nextIndex = (pIndex == game.Players.Count - 1) ? 0 : pIndex + 1;
                   Player next = game.Players[nextIndex];
                   if (game.RoleIdDict.ContainsKey(next.Id))
                   {
                       SendLangMessage(p.Id, game.GroupId,  Strings.AlreadySentRole, null, next.Name);
                   }
                   else
                   {
                       game.RoleIdDict.Add(next.Id, e.Message.Text);
                       SendLangMessage(p.Id, game.GroupId, Strings.SetRole, null, next.Name, e.Message.Text);
                       if (game.DictFull()) mre.Set();
                   }
               };
            try    //we don't wanna have that handler there if the thread is aborted, do we?
            {
                client.OnMessage += eHandler;
                mre.WaitOne();
            }
            finally
            {
                client.OnMessage -= eHandler;
            }
            SendLangMessage(game.GroupId,  Strings.AllRolesSet);
            game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
            foreach (Player p in game.Players)
            {
                string message = "\n";
                foreach (var kvp in game.RoleIdDict)
                {
                    if (kvp.Key != p.Id) message += $"{game.Players.Find(x => x.Id == kvp.Key).Name}: {kvp.Value}\n";
                }
                SendLangMessage(p.Id, game.GroupId, Strings.RolesAre, null, message);
            }
            #endregion
            int turn = 0;
            #region Player turns
            while (true)
            {
                // do players turns until everything is finished, then break;
                if (turn >= game.Players.Count) turn = 0;
                Player atTurn = game.Players[turn];
                game.Turn = atTurn;
                if (atTurn.GaveUp)
                {
                    game.Players.Remove(atTurn);
                    if (game.Players.Count > 0) continue;
                    else break;
                }
                SendAndGetLangMessage(game.GroupId, game.GroupId, Strings.PlayerTurn, null, out Message sentGroupMessage, out string uselessS, atTurn.Name);
                #region Ask Question
                string sentMessageText = "";
                Message sentMessage = null;
                EventHandler<MessageEventArgs> qHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.DeleteMessageAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                    SendAndGetLangMessage(atTurn.Id, game.GroupId, Strings.QuestionReceived, null,
                                out sentMessage, out var u);
                    string yes = GetString(Strings.Yes, LangCode(game.GroupId));
                    string idk = GetString(Strings.Idk, LangCode(game.GroupId));
                    string no = GetString(Strings.No, LangCode(game.GroupId));
                    client.DeleteMessageAsync(sentGroupMessage.Chat.Id, sentGroupMessage.MessageId);
                    SendAndGetLangMessage(game.GroupId, game.GroupId, Strings.QuestionAsked,
                        ReplyMarkupMaker.InlineYesNoIdk(yes, $"yes@{game.Id}", no, $"no@{game.Id}", idk, $"idk@{game.Id}"),
                        out sentGroupMessage, out sentMessageText,
                        $"<b>{WebUtility.HtmlEncode(atTurn.Name)}</b>", $"<i>{WebUtility.HtmlEncode(e.Message.Text)}</i>");
                    mre.Set();
                };
                bool guess = false;
                bool endloop = false;
                #region Guess handler
                EventHandler<MessageEventArgs> guessHandler = (sender, e) =>
                {
                    if (e.Message.From.Id != atTurn.Id || e.Message.Chat.Type != ChatType.Private) return;
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.DeleteMessageAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                    SendAndGetLangMessage(atTurn.Id, game.GroupId, Strings.QuestionReceived, null, out sentMessage, out var u);
                    string yes = GetString(Strings.Yes, LangCode(game.GroupId));
                    string no = GetString(Strings.No, LangCode(game.GroupId));
                    client.DeleteMessageAsync(sentGroupMessage.Chat.Id, sentGroupMessage.MessageId);
                    SendAndGetLangMessage(game.GroupId, game.GroupId, Strings.PlayerGuessed,
                        ReplyMarkupMaker.InlineYesNo(yes, $"yes@{game.Id}", no, $"no@{game.Id}"),
                        out sentGroupMessage, out sentMessageText,
                        atTurn.Name, e.Message.Text);
                    mre.Set();
                };
                #endregion
                #region Callback Handler
                EventHandler<CallbackQueryEventArgs> c1Handler = (sender, e) =>
                {
                    if (!game.Players.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("guess@") && !e.CallbackQuery.Data.StartsWith("giveup@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    long gameId = Convert.ToInt64(e.CallbackQuery.Data.Split('@')[1]);
                    if (gameId != game.Id) return;
                    if (e.CallbackQuery.Message == null) return;
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    Message cmsg = e.CallbackQuery.Message;
                    client.EditMessageReplyMarkupAsync(cmsg.Chat.Id, cmsg.MessageId);
                    switch (answer)
                    {
                        case "guess":
                            #region Guess
                            guess = true;
                            mre.Set();
                            EditLangMessage(e.CallbackQuery.From.Id, game.GroupId, sentMessage.MessageId, Strings.PleaseGuess, null, "",
                                out Message uselessM, out string uselessSS);
                            #endregion
                            break;
                        case "giveup":
                            #region Give Up
                            endloop = true;
                            Player p = game.Players.Find(x => x.Id == e.CallbackQuery.From.Id);
                            SendLangMessage(game.GroupId, Strings.GaveUp, null,
                                p.Name,
                                game.RoleIdDict[e.CallbackQuery.From.Id]);
                            SendLangMessage(p.Id, game.GroupId, Strings.YouGaveUp);
                            client.EditMessageReplyMarkupAsync(sentMessage.Chat.Id, sentMessage.MessageId);
                            game.Players.Remove(p);
                            mre.Set();
                            #endregion
                            break;
                    }
                };
                #endregion
                string guess1 = GetString(Strings.Guess, LangCode(game.GroupId));
                string giveUp1 = GetString(Strings.GiveUp, LangCode(game.GroupId));
                SendAndGetLangMessage(atTurn.Id, game.GroupId, Strings.AskQuestion,
                    ReplyMarkupMaker.InlineGuessGiveUp(guess1, $"guess@{game.Id}", giveUp1, $"giveup@{game.Id}"),
                    out sentMessage, out string useless);
                mre.Reset();
                try
                {
                    client.OnMessage += qHandler;
                    client.OnCallbackQuery += c1Handler;
                    mre.WaitOne();
                }
                finally
                {
                    client.OnMessage -= qHandler;
                    client.OnCallbackQuery -= c1Handler;
                }
                mre.Reset();
                game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                if (guess)
                {
                    try
                    {
                        client.OnMessage += guessHandler;
                        mre.WaitOne();
                    }
                    finally
                    {
                        client.OnMessage -= guessHandler;
                    }
                }
                if (game.Players.Count < 1) break;
                #endregion
                if (endloop) continue;
                #region Answer Question
                EventHandler<CallbackQueryEventArgs> cHandler = (sender, e) =>
                {
                    if (!game.TotalPlayers.Exists(x => x.Id == e.CallbackQuery.From.Id)
                    || (!e.CallbackQuery.Data.StartsWith("yes@") && !e.CallbackQuery.Data.StartsWith("no@") && !e.CallbackQuery.Data.StartsWith("idk@"))
                    || e.CallbackQuery.Data.IndexOf('@') != e.CallbackQuery.Data.LastIndexOf('@')) return;
                    if (e.CallbackQuery.Message == null) return;
                    string answer = e.CallbackQuery.Data.Split('@')[0];
                    if (!long.TryParse(e.CallbackQuery.Data.Split('@')[1], out long gameId)) return;
                    if (gameId != game.Id) return;
                    if (e.CallbackQuery.From.Id == atTurn?.Id && game.GroupId != testingGroupId)
                    {
                        client.AnswerCallbackQueryAsync(e.CallbackQuery.Id, GetString(Strings.NoAnswerSelf, LangCode(game.GroupId)), showAlert: true);
                        return;
                    }
                    game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                    client.AnswerCallbackQueryAsync(e.CallbackQuery.Id);
                    Message cmsg = e.CallbackQuery.Message;
                    string pname = game.TotalPlayers.Find(x => x.Id == e.CallbackQuery.From.Id).Name;
                    switch (answer)
                    {
                        case "yes":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId, Strings.AnsweredYes, null, sentMessageText + "\n",
                                out Message uselessM, out string uselessSS, pname);
                            EditLangMessage(sentMessage.Chat.Id, game.GroupId, sentMessage.MessageId,
                                Strings.AnsweredYes, null, "", out var u, out var u2, pname);
                            if (guess)
                            {
                                game.Players.Remove(atTurn);
                                game.TrySetWinner(atTurn);
                                SendLangMessage(game.GroupId, Strings.PlayerFinished, null,
                                    atTurn.Name);
                            }
                            break;
                        case "idk":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId,  Strings.AnsweredIdk, null, sentMessageText + "\n",
                                out Message uselessM2, out string uselessS2, pname);
                            EditLangMessage(sentMessage.Chat.Id, game.GroupId, sentMessage.MessageId,
                                 Strings.AnsweredIdk, null, "", out var u3, out var u4, pname);
                            break;
                        case "no":
                            EditLangMessage(game.GroupId, game.GroupId, cmsg.MessageId,  Strings.AnsweredNo, null, sentMessageText + "\n",
                                out Message uselessM1, out string uselessS1, pname);
                            EditLangMessage(sentMessage.Chat.Id, game.GroupId, sentMessage.MessageId,
                                 Strings.AnsweredNo, null, "", out var u5, out var u6, pname);
                            turn++;
                            break;
                    }
                    mre.Set();
                };
                mre.Reset();
                try
                {
                    client.OnCallbackQuery += cHandler;
                    mre.WaitOne();
                }
                finally
                {
                    client.OnCallbackQuery -= cHandler;
                }
                game.InactivityTimer.Change(game.Group.GameTimeout * 60 * 1000, Timeout.Infinite);
                if (game.Players.Count < 1) break;
                #endregion
            }
            #endregion
            #region Finish game
            game.InactivityTimer.Change(Timeout.Infinite, Timeout.Infinite);
            var par = new Dictionary<string, object>() { { "id", game.Id } };
            ExecuteSql("DELETE FROM Games WHERE Id=@id", par);
            long winnerId = game.Winner == null ? 0 : game.Winner.Id;
            string winnerName = game.Winner == null ? GetString(Strings.Nobody, LangCode(game.GroupId)) : game.Winner.Name;
            par = new Dictionary<string, object>() { { "groupid", game.GroupId }, { "winnerid", winnerId }, { "winnername", winnerName } };
            ExecuteSql("INSERT INTO GamesFinished (groupId, winnerid, winnername) VALUES(@groupid, @winnerid, @winnername)", par);
            SendLangMessage(game.GroupId, Strings.GameFinished, null, winnerName);
            SendLangMessage(game.GroupId, Strings.RolesWere, null, game.GetRolesAsString());
            GamesRunning.Remove(game);
            GameFinished?.Invoke(this, new GameFinishedEventArgs(game));
            #endregion
        }
        #endregion
        #region Cancel game
        private void CancelGame(Game g)
        {
            var par = new Dictionary<string, object>() { { "id", g.Id } };
            ExecuteSql($"DELETE FROM Games WHERE Id=@id", par);
            g.Thread?.Abort();
            GamesRunning.Remove(g);
            GameFinished?.Invoke(this, new GameFinishedEventArgs(g));
        }

        #endregion
        #endregion

        #region SQLite
        #region Execute SQLite Query
        private List<List<string>> ExecuteSql(string commandText, Dictionary<string, object> parameters = null)
        {
            List<List<string>> r = new List<List<string>>();
            using (var trans = sqliteConn.BeginTransaction())
            {
                using (var comm = new SQLiteCommand(commandText, sqliteConn, trans))
                {
                    if (parameters != null)
                    {
                        foreach (var kvp in parameters)
                        {
                            comm.Parameters.Add(new SQLiteParameter(kvp.Key, kvp.Value));
                        }
                    }
                    else if (commandText.Contains("@")) client.SendTextMessageAsync(Flom, $"Missing parameters in {commandText}");
                    try
                    {
                        using (var reader = comm.ExecuteReader())
                        {
                            /*if (!raw)
                            {
                                if (reader.RecordsAffected >= 0) r += $"_{reader.RecordsAffected} records affected_\n";
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += $"{reader.GetName(i)} ({reader.GetFieldType(i).Name})";
                                    if (i < reader.FieldCount - 1) r += " | ";
                                }
                                r += "\n\n";
                            }*/
                            while (reader.HasRows)
                            {
                                var row = new List<string>();
                                if (!reader.Read()) break;
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row.Add(reader.GetValue(i).ToString());
                                }
                                r.Add(row);
                            }
                        }
                    }
                    catch (SQLiteException x)
                    {
                        r = new List<List<string>>() { new List<string>() { x.Message } };
                    }
                }
                trans.Commit();
            }

            return r;
        }

        private string ExecuteSqlRaw(string commandText)
        {
            string r = "";
            using (var trans = sqliteConn.BeginTransaction())
            {
                using (var comm = new SQLiteCommand(commandText, sqliteConn, trans))
                {
                    try
                    {
                        using (var reader = comm.ExecuteReader())
                        {
                            if (reader.RecordsAffected >= 0) r += $"<i>{reader.RecordsAffected} records affected</i>\n";
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                r += $"{reader.GetName(i)} ({reader.GetFieldType(i).Name})";
                                if (i < reader.FieldCount - 1) r += " | ";
                            }
                            r += "\n\n";
                            while (reader.HasRows)
                            {
                                if (!reader.Read()) break;
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    r += reader.GetValue(i);
                                    if (i < reader.FieldCount - 1) r += " | ";
                                }
                                r += "\n";
                            }
                        }
                    }
                    catch (SQLiteException x)
                    {
                        r = x.Message;
                    }
                }
                trans.Commit();
            }

            return r;
        }
        #endregion
        #endregion
    }
}
