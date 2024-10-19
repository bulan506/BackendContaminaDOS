using System;
using System.Collections.Generic;
public class Round
{
    public string id { get; set; }
    public string gameId { get; set; } // ID del juego al que pertenece
    public string leader { get; set; } // Nombre del líder del round
    public string status { get; set; } // Estado del round
    public string result { get; set; } // Resultado del round
    public string phase { get; set; } // Fase actual del round
    public List<string> group { get; set; } // Lista de miembros del grupo
    public List<bool> votes { get; set; } // Lista de votos
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }

}
