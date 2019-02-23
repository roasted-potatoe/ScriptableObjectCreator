using UnityEngine;

namespace Enemy
{
	public class EnemyData : ScriptableObject
	{
		[SerializeField]
		private int m_Health;

		[SerializeField]
		private float m_AttackStrength;
	}
}