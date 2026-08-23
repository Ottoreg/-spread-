using Godot;

/// <summary>
/// Machine à états légère, encodée dans byte[] State + float[] StateTimer.
/// Le prototype alterne deux états (0/1) pour démontrer le coût d'une FSM
/// data-driven sur toute la population et pour colorer les entités par état.
///
/// Volontairement sans RNG dans la boucle (déterministe, zéro allocation) :
/// la durée d'état dérive de l'indice, ce qui désynchronise naturellement les
/// entités sans coût.
/// </summary>
public static class StateMachineSystem
{
    public static void Run(Simulation sim, float dt)
    {
        int count = sim.Count;
        var state = sim.State;
        var timer = sim.StateTimer;

        for (int i = 0; i < count; i++)
        {
            timer[i] -= dt;
            if (timer[i] <= 0f)
            {
                state[i] = (byte)(state[i] == 0 ? 1 : 0);
                timer[i] = 2f + (i % 7) * 0.4f;
            }
        }
    }
}
