﻿using System;
using System.Collections.Generic;
using System.Linq;
using Quaver.API.Enums;
using Quaver.Config;
using Quaver.Database.Maps;
using Quaver.Database.Scores;

namespace Quaver.Screens.Select.UI.MapInfo.Leaderboards.Scores
{
    public class LeaderboardSectionLocal : LeaderboardSectionScores
    {
        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="leaderboard"></param>
        public LeaderboardSectionLocal(Leaderboard leaderboard) : base(LeaderboardSectionType.Local, leaderboard, "Local")
        {
            ScrollContainer.Alpha = 0;
            FetchAndUpdateLeaderboards(MapManager.Selected.Value.Scores.Value);
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <returns></returns>
        protected sealed override List<LocalScore> FetchScores() => LocalScoreCache.FetchMapScores(MapManager.Selected.Value.Md5Checksum);
    }
}