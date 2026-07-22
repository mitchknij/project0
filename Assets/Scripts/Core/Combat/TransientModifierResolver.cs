using System;
using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class TransientModifierResolver
    {
        public static double Compose(
            double persistentValue,
            CombatStatProperty property,
            string actorId,
            long simulationTick,
            IReadOnlyList<TransientCombatModifier> modifiers,
            double minimum,
            double maximum)
        {
            var ordered = new List<TransientCombatModifier>();
            foreach (TransientCombatModifier modifier in modifiers ?? Array.Empty<TransientCombatModifier>())
                if (modifier != null && modifier.Property == property && modifier.TargetActorId == actorId &&
                    modifier.StartTick <= simulationTick && simulationTick < modifier.EndTick)
                    ordered.Add(modifier);
            ordered.Sort((left, right) =>
            {
                int definitionOrder = string.CompareOrdinal(left.DefinitionId, right.DefinitionId);
                return definitionOrder != 0
                    ? definitionOrder
                    : left.ApplicationSequenceId.CompareTo(right.ApplicationSequenceId);
            });

            double value = persistentValue;
            foreach (TransientCombatModifier modifier in ordered)
                if (modifier.Operation == CombatModifierOperation.FlatAdd) value += modifier.Magnitude;

            double additivePercent = 0.0;
            foreach (TransientCombatModifier modifier in ordered)
                if (modifier.Operation == CombatModifierOperation.AdditivePercent)
                    additivePercent += modifier.Magnitude;
            value *= 1.0 + additivePercent;

            foreach (TransientCombatModifier modifier in ordered)
                if (modifier.Operation == CombatModifierOperation.MultiplicativePercent)
                    value *= 1.0 + modifier.Magnitude;

            foreach (TransientCombatModifier modifier in ordered)
                if (modifier.Operation == CombatModifierOperation.Override)
                    value = modifier.Magnitude;

            return CombatMath.Clamp(value, minimum, maximum);
        }
    }
}
