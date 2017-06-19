// ====================================================================================================================
//
// Created by Leslie Young
// http://www.plyoung.com/ or http://plyoung.wordpress.com/
// ====================================================================================================================

using UnityEngine;

public class NaviUnit : MonoBehaviour 
{
	// ====================================================================================================================
	#region inspector properties

	// the level/layer this unit can occupy on a tile, see TileNode.units for more info
	public TileNode.TileType tileLevel = TileNode.TileType.Normal;

	// set to true for units that "jump" from node to node when moving
	public bool jumpMovement = false;

	// how fast a unit will be move from tile to tile
	public float moveSpeed = 2f;

	// Multiplayer used with moveSpeed. Setting this will make a unit move faster
	// from node to node when it has to move further, so as not to bore players ;)
	public float moveSpeedMulti = 0.1f;

	// --------------------------------------------------
	// the following are not used when jumpMovement is on

	// should unit tilt (look up/down) when moving from node-to-node that are not on equel level?
	public bool tiltToPath = false;

	// these are used to have the unit adjust as needed. Usefull on uneven terrain where the unit might go through
	// terrain if only following nodes blindly or if you would like it to rotate according to the terrain normals
	public bool adjustToCollider = false;
	public bool adjustNormals = false; // if this is true then adjustToCollider will be performed too
	public LayerMask adjustmentLayerMask = 0;

	// --------------------------------------------------

	#endregion
	// ====================================================================================================================
	#region vars

	// node that this unit is currently standing on
	public TileNode node { get; set; }

	// callbacks for when unit completed a task
	// eventCode 1 = movement to node completed
	public delegate void UnitEventDelegate(NaviUnit unit, int eventCode);
	protected UnitEventDelegate onUnitEvent = null;

	protected bool isMoving = false;					// unit is moving, this.node will allready be set to the destination node
	protected Vector3 endPointToReach = new Vector3();	// where unit is trying to get to

	protected Transform _tr; // cached transform

	#endregion
	// ====================================================================================================================
	#region pub

	/// <summary>Instantiates a new unit on target node</summary>
	public static NaviUnit SpawnUnit(GameObject unitFab, TileNode node)
	{
		GameObject go = (GameObject)GameObject.Instantiate(unitFab);
		go.transform.position = node.transform.position;
		NaviUnit unit = go.GetComponent<NaviUnit>();
		unit.LinkWith(node);
		return unit;
	}

	/// <summary>Init a unit. You normally want to do this right after it was spawned</summary>
	/// <param name="callback">Optional callback to call after tasks, like moving to a node, are completed</param>
	public virtual void Init(UnitEventDelegate callback)
	{
		this.onUnitEvent = callback;
	}

	public virtual void Start()
	{
		this._tr = gameObject.transform;
	}

	// ====================================================================================================================

	/// <summary>
	/// Link with target node and unlink with any currently linked node. aka, set NaviUnit.node variable.
	/// Note that this does not check if targetNode is a valid node that this Unit could stand on.
	/// Use CanStandOn to check for that.
	/// </summary>
	public void LinkWith(TileNode targetNode)
	{
		// first unlink with previous node if standing on any
		if (this.node != null)
		{
			this.node.units.Remove(this);
		}

		// link with new node
		this.node = targetNode;
		if (this.node != null)
		{
			this.node.units.Add(this);
		}
	}

	/// <summary>
	/// checks if unit can stand on the target node
	/// </summary>
	/// <param name="andLevelIsOpen">also check if there is a unit in the way?</param>
	public bool CanStandOn(TileNode targetNode, bool andLevelIsOpen)
	{
		if (targetNode == null) return false;

		// can this unit move onto a lvel on this tile?
		if ((targetNode.tileTypeMask & this.tileLevel) == this.tileLevel)
		{
			// check if there might be unit in the way on target tile?
			if (andLevelIsOpen)
			{
				// success if there is not a unit standing in same level on target tilenode
				if (targetNode.GetUnitInLevel(this.tileLevel) == null) return true;
			}
			else
			{
				return true;
			}
		}

		// fail
		return false;
	}

	// ====================================================================================================================

	/// <summary>called when movement completed. override if you want to change playing animation for example</summary>
	protected virtual void OnMovementComplete() { }

	/// <summary>return a list if nodes that can be used to reach the target node</summary>
	public TileNode[] GetPathTo(MapNav map, TileNode targetNode)
	{
		return map.GetPath(this.node, targetNode, this.tileLevel);
	}

	/// <summary>
	/// Move the unit to the specified target node. Will make a callback with 
	/// eventCode=1 when done moving if a callback was set in Init()
	/// iTween (free on Unity Asset Store) is used to move the Unit
	/// </summary>
	/// <param name="map">NavMap to use</param>
	/// <param name="targetNode">Node to reach</param>
	/// <param name="moves">
	/// Number of moves that can be use to reach target. Pass (-1) to ignore this. 
	/// The unit will move as close as possible if not enough moves given.
	/// Moves will be set to moves - actual_moves_use; or if -1 passed, it will be set to number of moves that was taken.
	/// </param>
	/// <returns>True is the unit started moving towards the target</returns>
	public bool MoveTo(MapNav map, TileNode targetNode, ref int moves)
	{
		if (moves == 0) return false;

		TileNode[] path = map.GetPath(this.node, targetNode, this.tileLevel);
		if (path == null) return false;
		if (path.Length == 0) return false;

		// check if enough moves to reach destination
		int useMoves = path.Length;
		if (moves >= 0)
		{
			if (moves <= useMoves) { useMoves = moves; moves = 0; }
			else moves -= useMoves;
		}
		else moves = path.Length;

		// unlink with current node and link with destination node
		this.node.units.Remove(this);
		this.node = path[useMoves-1];
		this.node.units.Add(this);

		isMoving = true;

		// *** start jumping from tile to tile
		if (jumpMovement)
		{
			_jump_points = new Vector3[useMoves];
			_jump_pointIdx = 0;
			for (int i = 0; i < useMoves; i++) _jump_points[i] = path[i].transform.position;
			_JumpToNextPoint();
		}

		// *** start normal movement from tile to tile
		else
		{
			System.Collections.Hashtable opt = iTween.Hash(
						"orienttopath", true,
						"easetype", "linear",
						"oncomplete", "_OnCompleteMoveTo",
						"oncompletetarget", gameObject);

			if (!tiltToPath) opt.Add("axis", "y");

			if ((adjustNormals || adjustToCollider) && adjustmentLayerMask != 0)
			{
				opt.Add("onupdate", "_OnMoveToUpdate");
				opt.Add("onupdatetarget", gameObject);
			}

			if (path.Length > 1)
			{
				Vector3[] points = new Vector3[useMoves];
				for (int i = 0; i < useMoves; i++) points[i] = path[i].transform.position;
				endPointToReach = points[points.Length - 1];
				opt.Add("path", points);
				opt.Add("speed", ((moveSpeedMulti * useMoves) + 1f) * moveSpeed);
				iTween.MoveTo(this.gameObject, opt);
			}
			else
			{
				endPointToReach = path[0].transform.position;
				opt.Add("position", endPointToReach);
				opt.Add("speed", moveSpeed);
				iTween.MoveTo(this.gameObject, opt);
			}
		}

		return true;
	}

	#endregion
	// ====================================================================================================================
	#region internal

	private Vector3[] _jump_points=null;
	private int _jump_pointIdx=0;
	private void _JumpToNextPoint()
	{
		endPointToReach = _jump_points[_jump_pointIdx];

		float dist = Vector3.Distance(_tr.position, endPointToReach);
		Vector3[] points = new Vector3[3];
		points[0] = _tr.position;
		points[1] = Vector3.MoveTowards(points[0], endPointToReach, dist / 2f);
		float h = Mathf.Abs(points[0].y - endPointToReach.y) * 2f;
		if (h <= 0.01f) h = dist / 2f;
		points[1].y += h;
		points[2] = endPointToReach;

		iTween.MoveTo(this.gameObject, iTween.Hash(
						"path", points,
						"speed", moveSpeed,
						"orienttopath", true,
						"axis", "y",
						"easetype", "easeInOutExpo",
						"oncomplete", "_OnCompleteJump",
						"oncompletetarget", gameObject
						));
	}

	private void _OnCompleteJump()
	{
		_jump_pointIdx++;
		if (_jump_pointIdx < _jump_points.Length)
		{	// jump to next node
			_JumpToNextPoint();
		}

		else
		{	// reached destination
			isMoving = false;
			this.OnMovementComplete();
			if (onUnitEvent != null) onUnitEvent(this, 1);
		}
	}

	private void _OnCompleteMoveTo()
	{
		this.UpdateUnitNormal();

		isMoving = false;
		this.OnMovementComplete();
		if (onUnitEvent != null) onUnitEvent(this, 1);
	}

	private void _OnMoveToUpdate()
	{
		this.UpdateUnitNormal();
	}

	private Vector3 _smoothedNormal = Vector3.up;
	private void UpdateUnitNormal()
	{
		if ( (!adjustNormals && !adjustToCollider) || adjustmentLayerMask==0 ) return;

		Vector3 pos = _tr.position; pos.y += 10f;
		RaycastHit hit;
		if (Physics.Raycast(pos, -Vector3.up, out hit, 50f, adjustmentLayerMask))
		{
			pos.y -= hit.distance; pos.y += 0.01f;
			_tr.position = pos;

			if (adjustNormals)
			{
				_smoothedNormal = Vector3.Lerp(_smoothedNormal, hit.normal, 10f * Time.deltaTime);
				Quaternion tilt = Quaternion.FromToRotation(Vector3.up, _smoothedNormal);
				transform.rotation = tilt * Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
			}
		}
	}

	#endregion
	// ====================================================================================================================
}
