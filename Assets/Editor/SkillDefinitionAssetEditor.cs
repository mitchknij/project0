using IdleCloud.Core;
using IdleCloud.Managers;
using UnityEditor;

namespace IdleCloud.Editor
{
    [CustomEditor(typeof(SkillDefinitionAsset))]
    internal sealed class SkillDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("stableId"); Draw("displayName"); Draw("description"); Draw("element"); Draw("mechanic");
            Draw("cooldownMilliseconds"); Draw("damageMultiplier"); Draw("aoeColor"); Draw("priority");
            Draw("targeting"); Draw("minimumAutoTargets"); Draw("autoEnabled");

            SkillTargetingKind targeting = (SkillTargetingKind)serializedObject.FindProperty("targeting").enumValueIndex;
            bool tile = targeting == SkillTargetingKind.TilePatternAroundSource || targeting == SkillTargetingKind.TilePatternAroundTarget;
            bool circle = targeting == SkillTargetingKind.CircleAroundSource || targeting == SkillTargetingKind.CircleAroundTarget;
            bool actor = targeting == SkillTargetingKind.HostileActor;
            if (tile) Draw("tilePattern");
            if (circle) Draw("radiusWorldUnits");
            if (actor) Draw("rangePixels");

            SkillMechanic mechanic = (SkillMechanic)serializedObject.FindProperty("mechanic").enumValueIndex;
            if (mechanic == SkillMechanic.Projectile) { Draw("projectileSpeed"); Draw("projectileRadius"); }
            if (mechanic == SkillMechanic.Buff)
            {
                Draw("modifierProperty"); Draw("modifierOperation"); Draw("modifierMagnitude"); Draw("modifierDurationTicks");
            }
            Draw("timing");
            if ((SkillTimingKind)serializedObject.FindProperty("timing").enumValueIndex == SkillTimingKind.ScheduledImpact) Draw("impactDelayTicks");
            if (mechanic == SkillMechanic.Aoe || mechanic == SkillMechanic.Projectile || mechanic == SkillMechanic.Debuff) Draw("inflicts", true);
            Draw("prerequisiteSkillId"); Draw("branchId"); Draw("tier"); Draw("skillPointCost"); Draw("allowedClasses", true);
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string property, bool includeChildren = false) => EditorGUILayout.PropertyField(serializedObject.FindProperty(property), includeChildren);
    }
}
