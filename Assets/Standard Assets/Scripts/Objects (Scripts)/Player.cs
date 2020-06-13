using UnityEngine;
using Extensions;

namespace GridGame
{
	public class Player : Entity, ISaveableAndLoadable
	{
		int moveInput;
		int previousMoveInput;
		public delegate void OnMoved();
		public event OnMoved onMoved;
		public Transform hpIcon;
		public Transform hpIconParent;
		public LayerMask whatIsSafeZone;
		public LayerMask whatIsDangerZone;
		public LayerMask whatIsScroll;
		public LayerMask whatIsSavePoint;
		public LayerMask whatIsBullet;
		Scroll currentlyReading;
		public static DangerArea currentDangerArea;
		bool inSafeZone;
		public int uniqueId;
		[SaveAndLoadValue(false)]
		public Vector2 SpawnPosition
		{
			get
			{
				return trs.position;
			}
			set
			{
				trs.position = value;
			}
		}
		public int UniqueId
		{
			get
			{
				return uniqueId;
			}
			set
			{
				uniqueId = value;
			}
		}

		public override void OnEnable ()
		{
			base.OnEnable ();
			hp = maxHp;
			for (int i = 1; i < hp; i ++)
				Instantiate(hpIcon, hpIconParent);
		}

		public override void DoUpdate ()
		{
			if (GameManager.paused)
				return;
			moveInput = InputManager.MoveInput;
			HandleMoving ();
			previousMoveInput = moveInput;
		}

		public override void HandleMoving ()
		{
			if (moveInput > previousMoveInput)
			{
				for (int i = 0; i < moveInput - previousMoveInput; i ++)
				{
					Vector2 desiredMove = GameManager.GetSingleton<GameCamera>().camera.ScreenToWorldPoint(InputManager.MousePosition) - trs.position;
					int indexOfClosestPossibleMove = desiredMove.GetIndexOfClosestPoint(GameManager.GetSingleton<GameManager>().possibleMoves);
					Vector2 move = GameManager.GetSingleton<GameManager>().possibleMoves[indexOfClosestPossibleMove];
					if (Physics2D.OverlapPoint((Vector2) trs.position + move, whatICantMoveTo) == null)
						Move (move);
				}
			}
		}

		public override bool Move (Vector2 move)
		{
			if (base.Move(move))
			{
				OnMove ();
				return true;
			}
			return false;
		}

		public virtual void OnMove ()
		{
			if (onMoved != null)
				onMoved ();
			inSafeZone = CheckForSafeZone ();
			if (!inSafeZone)
				CheckForDangerZone ();
			CheckForScroll ();
		}

		public virtual bool CheckForSafeZone ()
		{
			Collider2D hitCollider = Physics2D.OverlapPoint(trs.position, whatIsSafeZone);
			if (hitCollider != null)
			{
				foreach (Enemy enemy in Enemy.enemiesInArea)
				{
					enemy.Reset ();
					enemy.enabled = false;
				}
				Enemy.enemiesInArea = new Enemy[0];
				foreach (Trap trap in Trap.trapsInArea)
				{
					trap.Reset ();
					trap.enabled = false;
				}
				Trap.trapsInArea = new Trap[0];
                for (int i = 0; i < Bullet.activeBullets.Count; i ++)
                {
                    Bullet bullet = Bullet.activeBullets[i];
                    GameManager.GetSingleton<ObjectPool>().Despawn (bullet.prefabIndex, bullet.gameObject, bullet.trs);
					i --;
                }
                SafeArea safeArea = hitCollider.GetComponent<SafeZone>().safeArea;
				GameManager.GetSingleton<GameCamera>().trs.position = safeArea.cameraRect.center.SetZ(GameManager.GetSingleton<GameCamera>().trs.position.z);
				GameManager.GetSingleton<GameCamera>().viewSize = safeArea.cameraRect.size;
				GameManager.GetSingleton<GameCamera>().HandleViewSize ();
				return true;
			}
			return false;
		}

		public virtual bool CheckForDangerZone ()
		{
			Collider2D hitCollider = Physics2D.OverlapPoint(trs.position, whatIsDangerZone);
			if (hitCollider != null)
			{
				if (Enemy.enemiesInArea.Length == 0 && Trap.trapsInArea.Length == 0)
				{
					currentDangerArea = hitCollider.GetComponent<DangerZone>().dangerArea;
					Enemy.enemiesInArea = currentDangerArea.enemies;
					foreach (Enemy enemy in Enemy.enemiesInArea)
						enemy.enabled = true;
					Trap.trapsInArea = currentDangerArea.traps;
					foreach (Trap trap in Trap.trapsInArea)
						trap.enabled = true;
					GameManager.GetSingleton<GameCamera>().trs.position = currentDangerArea.cameraRect.center.SetZ(GameManager.GetSingleton<GameCamera>().trs.position.z);
					GameManager.GetSingleton<GameCamera>().viewSize = currentDangerArea.cameraRect.size;
					GameManager.GetSingleton<GameCamera>().HandleViewSize ();
				}
				return true;
			}
			return false;
		}

		public virtual bool CheckForScroll ()
		{
			Collider2D hitCollider = Physics2D.OverlapPoint(trs.position, whatIsScroll);
			if (hitCollider != null)
			{
				currentlyReading = hitCollider.GetComponent<Scroll>();
				currentlyReading.displayText.text.text = currentlyReading.text;
				currentlyReading.displayText.text.enabled = true;
				return true;
			}
			else if (currentlyReading != null)
				currentlyReading.displayText.text.enabled = false;
			return false;
		}

		public virtual bool CheckForSavePoint ()
		{
			Collider2D hitCollider = Physics2D.OverlapPoint(trs.position, whatIsSavePoint);
			if (hitCollider != null)
			{
				hp = maxHp;
				SpawnPosition = trs.position;
				GameManager.GetSingleton<SaveAndLoadManager>().Save ();
				return true;
			}
			return false;
		}

		public virtual bool CheckForBullet ()
		{
			Collider2D hitCollider = Physics2D.OverlapPoint(trs.position, whatIsBullet);
			if (hitCollider != null)
			{
				Bullet bullet = hitCollider.GetComponent<Bullet>();
				TakeDamage (bullet.damage);
				bullet.Death ();
				return true;
			}
			return false;
		}

		public override void TakeDamage (float amount)
		{
			if ((int) (hp - amount) < (int) hp)
				Destroy(hpIconParent.GetChild(0).gameObject);
			base.TakeDamage (amount);
		}

		public override void Death ()
		{
			GameManager.paused = true;
			base.Death ();
			for (int i = 0; i < EventManager.events.Count; i ++)
				EventManager.events[i].time = Mathf.Infinity;
			EventManager.events.Clear();
			Enemy.enemiesInArea = new Enemy[0];
			Trap.trapsInArea = new Trap[0];
			GameManager.GetSingleton<GameManager>().ReloadActiveScene ();
		}
	}
}