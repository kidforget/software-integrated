// ====================================================================================================================
//
// Created by Leslie Young
// http://www.plyoung.com/ or http://plyoung.wordpress.com/
// ====================================================================================================================

using System.Collections;
using UnityEngine;

public class Unit : NaviUnit
{
	// ====================================================================================================================
	#region inspector properties

	public int playerSide = 1;			// player-1 and player-2
	public int maxMoves = 1;			// how far this unit can move per turn
	public int attackRange = 1;			// range it can attack at
	public int attackDamage = 1;		// damage caused by an attack
	public Vector3 targetingOffset = Vector3.zero; // where missile should hit it
    public int HP = 100;
	#endregion
	// ====================================================================================================================
	#region vars

	[HideInInspector]
	public int currMoves = 0;			// how many moves this unit has left

	public bool didAttack { get; set; }

	private SampleWeapon weapon;

	#endregion
	// ====================================================================================================================
	#region pub

	public override void Start()
	{
		base.Start();
		weapon = gameObject.GetComponent<SampleWeapon>();
		weapon.Init(OnAttackDone);
	}

	/// <summary>Should be called right after unit was spawned</summary>
	public override void Init(UnitEventDelegate callback)
	{
		base.Init(callback);
		this.Reset();		
	}

	/// <summary>Reset some values</summary>
	public void Reset()
	{
		currMoves = maxMoves;
		didAttack = false;
	}

	/// <summary>Check if target can be attacked by this unit</summary>
	public bool CanAttack(Unit target)
	{
		if (didAttack) return false; // can't attack again in this turn
		if (target.playerSide == this.playerSide) return false; // can't attack a friend
		if (this.node.units.Contains(target)) return false; // can't shoot at unit on same tile, for eample a flying unit over opponent land unit

		// finally check if target is in range
		return this.node.IsInRange(target.node, this.attackRange);
	}

	/// <summary>Makes an attack on the target. Unit event callback will be passed an eventCode of 2</summary>
	public bool Attack(Unit target)
	{
		if (!CanAttack(target)) return false;

		didAttack = true;

		// turn to face target
		Vector3 direction = target.transform.position - transform.position; direction.y = 0f;
		transform.rotation = Quaternion.LookRotation(direction);

		weapon.Play(target);
        int del = Random.Range(30, 40);
        target.UnderAttack(del);
		return true;
	}
    private int ddd;
    IEnumerator MyMethod()
    {
        Debug.Log("Before Waiting 2 seconds");
        yield return new WaitForSeconds(1);
        Debug.Log("After Waiting 2 Seconds");


        HP -= ddd;
        if (HP <= 0)
        {
            HP = 0;
        }
    }
    public void UnderAttack(int del)
    {
        ddd = del;
        StartCoroutine("MyMethod");
        //MyMethod();
        //for (int i = 1; i <= 200000000; i++) tot++;
        

        if (HP <= 0)
        {
            HP = 0;
            //this.node.units.Remove(this);
            //transform.position = new Vector3(0, 0, 0);
            //float x = transform.position.x;
            //float y = transform.position.y;
            //float z = transform.position.z;
            //transform.position.Set(x, y+100, z);
        }
    }



	/// <summary>called by the weapon when it is done doing its thing</summary>
	private void OnAttackDone(NaviUnit unit, int eventCode)
	{
		// tell whomever is listening that I am done with my attack. eventCode 2
		if (onUnitEvent != null) onUnitEvent(this, 2);
	}

	#endregion
	// ====================================================================================================================
}
