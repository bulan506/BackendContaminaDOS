﻿using System;
using System.Collections.Generic;

public partial class ActionRound
{
    public string id { get; set; }

    public string? gameId { get; set; }

    public string? roundId { get; set; }

    public string? playerId { get; set; }

    public bool? actionRound { get; set; }

    public virtual Game? game { get; set; }

    public virtual Player? player { get; set; }

    public virtual Round? round { get; set; }
}
