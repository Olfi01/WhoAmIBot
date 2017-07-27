﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace WhoAmIBotSpace.Helpers
{
    public static class ReplyMarkupMaker
    {
        public static IReplyMarkup InlineYesNo(string yes, string yesCallback, string no, string noCallback)
        {
            InlineKeyboardButton yesButton = new InlineKeyboardButton(yes, yesCallback);
            InlineKeyboardButton noButton = new InlineKeyboardButton(no, noCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { yesButton, noButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }

        public static IReplyMarkup InlineGuessGiveUp(string guess, string guessCallback, string giveUp, string giveUpCallback)
        {
            InlineKeyboardButton guessButton = new InlineKeyboardButton(guess, guessCallback);
            InlineKeyboardButton giveUpButton = new InlineKeyboardButton(giveUp, giveUpCallback);
            InlineKeyboardButton[] row = new InlineKeyboardButton[] { guessButton, giveUpButton };
            return new InlineKeyboardMarkup(new InlineKeyboardButton[][] { row });
        }
    }
}