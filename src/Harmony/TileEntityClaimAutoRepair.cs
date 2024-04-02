using UnityEngine;
using System.Collections.Generic;
using System;
using static Block;
using UnityEngine.Assertions;

public class TileEntityClaimAutoRepair : TileEntitySecureLootContainer
{
	//// how much damage repair per tick
	//// Gets multiplied by Time.deltaTime!
	//// 750f ~> 2000 hit-points in 1 in-game hour
	//// 750f ~> 10k hit-points in 14m real-time
	//// 750f ~> 48k hit-points in 1 in-game day
	//// 750f ~> each tick fixes ~25 hit-points
	//public float repairSpeed = 2000f;

	//// The acquired block to be repaired
	//public BlockValue repairBlock;

	//// The position of the block being repaired
	//// To check if block is still the same in the world
	//public Vector3i repairPosition;

	//// How much damage has already been repaired
	//// To check when the block is fully repaired
	//public float repairDamage;

	//// Percentage of damage on acquired block
	//// To calculate amount of items needed for repair
	//public float damagePerc;

	// Flag only for server side code
	public bool isAccessed;

	private string lastMissingItem = null;

	// Copied from LandClaim code
	public Transform BoundsHelper;

	//private Color lastColor = Color.clear;

	// TODO: move these constants into config.xml
	const int	COOLDOWN		= 5;
	const int	MAX_ITERTIONS	= 1000;
	const bool	NEEDS_MATERIAL	= true;

	private bool __isOn;

	public bool IsOn
	{
		get => this.__isOn;
		set
		{
			if (this.__isOn != value)
			{
				this.__isOn = value;
				//repairBlock = BlockValue.Air;
				//repairPosition = ToWorldPos();
				//damagePerc = 0.0f;
				//repairDamage = 0.0f;
				ResetBoundHelper(Color.gray);
				SetModified();
			}
		}
	}

	public TileEntityClaimAutoRepair(Chunk _chunk) : base(_chunk)
	{
		__isOn = false;
		isAccessed = false;
		//repairBlock = BlockValue.Air;
		//repairDamage = 0.0f;
		//damagePerc = 0.0f;
	}

	public override TileEntityType GetTileEntityType() => (TileEntityType)242;

	public int ReduceItemCount(string item_name, int item_count)
	{
		// TODO: optimize this function, by caching 'this.items' in a Hashed structure
		// -> purpose: prevents from iterating over each 'this.items'

		int needed_item_count = item_count;

		for (int i = 0; i < this.items.Length; i++)
		{
			ItemStack stack = this.items[i];

			if (stack.IsEmpty())
				continue;

			// TODO: how expensive is this call for `GetItem(string)`? (see ItemClass.GetItemClass to get an idea)
			// TODO: check if this attribute can do the job in a more efficient way: stack.itemValue.ItemClass.Name
			if (stack.itemValue.type != ItemClass.GetItem(item_name).type)
				continue;

			int taken_items_count = Math.Min(stack.count, needed_item_count);

			needed_item_count -= taken_items_count;
			stack.count -= taken_items_count;

			if (needed_item_count < 0)
				Log.Error($"TileEntityClaimAutoRepair.ReduceItemCount: needed_item_count < 0 (={needed_item_count})");

			if (stack.count < 0)
				Log.Error($"TileEntityClaimAutoRepair.ReduceItemCount: stack.count  < 0 (={stack.count})");

			UpdateSlot(i, stack);

			if (needed_item_count == 0)
				break;
		}

		return needed_item_count;
	}

	public Dictionary<string, int> TakeRepairMaterials(float damages_perc, List<SItemNameCount> repair_items)
	{
		if (repair_items == null)
			return null;

		Dictionary<string, int> missing_materials = new Dictionary<string, int>();

		foreach (SItemNameCount item in repair_items)
		{
			int required_item_count = (int) Mathf.Ceil(item.Count * damages_perc);
			int missing_item_count = ReduceItemCount(item.ItemName, required_item_count);

			//Log.Out($"{item.ItemName}: required_material={required_item_count} (={item.Count} * {damages_perc:F3})");

			if (missing_item_count > 0)
				missing_materials.Add(item.ItemName, missing_item_count);
		}

		return missing_materials.Count > 0 ? missing_materials : null;
	}

	private List<Vector3i> get_neighbors(Vector3i pos)
	{
		return new List<Vector3i>
		{
			new Vector3i(pos.x + 1, pos.y, pos.z),
			new Vector3i(pos.x - 1, pos.y, pos.z),

			new Vector3i(pos.x, pos.y + 1, pos.z),
			new Vector3i(pos.x, pos.y - 1, pos.z),

			new Vector3i(pos.x, pos.y, pos.z + 1),
			new Vector3i(pos.x, pos.y, pos.z - 1),

			new Vector3i(pos.x, pos.y + 1, pos.z + 1),
			new Vector3i(pos.x, pos.y - 1, pos.z + 1),
			new Vector3i(pos.x, pos.y + 1, pos.z - 1),
			new Vector3i(pos.x, pos.y - 1, pos.z - 1),

			new Vector3i(pos.x + 1, pos.y, pos.z + 1),
			new Vector3i(pos.x - 1, pos.y, pos.z + 1),
			new Vector3i(pos.x + 1, pos.y, pos.z - 1),
			new Vector3i(pos.x - 1, pos.y, pos.z - 1),

			new Vector3i(pos.x + 1, pos.y + 1, pos.z),
			new Vector3i(pos.x + 1, pos.y - 1, pos.z),
			new Vector3i(pos.x - 1, pos.y + 1, pos.z),
			new Vector3i(pos.x - 1, pos.y - 1, pos.z),

			new Vector3i(pos.x + 1, pos.y + 1, pos.z + 1),
			new Vector3i(pos.x + 1, pos.y - 1, pos.z + 1),
			new Vector3i(pos.x + 1, pos.y + 1, pos.z - 1),
			new Vector3i(pos.x + 1, pos.y - 1, pos.z - 1),

			new Vector3i(pos.x - 1, pos.y + 1, pos.z + 1),
			new Vector3i(pos.x - 1, pos.y - 1, pos.z + 1),
			new Vector3i(pos.x - 1, pos.y + 1, pos.z - 1),
			new Vector3i(pos.x - 1, pos.y - 1, pos.z - 1),
		};
	}

	private int compute_damage(BlockValue block, Dictionary<string, int> missing_materials)
	{
		if(missing_materials == null || block.Block.RepairItems == null)
			return 0;

		float total_required = 0.0f;
		float total_missing = 0.0f;
		float damage_perc = (float)block.damage / block.Block.MaxDamage;

		foreach(SItemNameCount item in block.Block.RepairItems)
		{
			total_required += Mathf.Ceil(item.Count * damage_perc);

			if (!missing_materials.ContainsKey(item.ItemName))
				continue;

			total_missing += missing_materials[item.ItemName];
		}

		// Log.Out($"{block.Block.GetBlockName()}.total_required: {total_required}");

		int computed_damages = (int)Mathf.Ceil(block.damage * total_missing / total_required);

		// Log.Out($"Computed damage: {computed_damages}, = {block.damage} * {total_missing} / {total_required}");

		return computed_damages;
	}

	private Dictionary<string, int> repair_block(World world, Vector3i pos)
	{

		BlockValue block = world.GetBlock(pos);
		Dictionary<string, int> missing_items = null;

		// TODO: what is the purpose of this condition ?
		if (world.GetChunkFromWorldPos(pos) is Chunk chunkFromWorldPos)
		{

			// TODO: find a better way to compute the needed repair_items for spike blocks
			// (for now, if a spike is at stage Dmg1 or Dmg2 with damage=0, the upgrade to Dmg0 is free)
			List<SItemNameCount> repair_items = block.Block.RepairItems;

			const uint trapSpikesWoodDmg0_id = 21469;
			const uint trapSpikesIronDmg0_id = 21476;

			// handle repairing of spike blocks
			switch (block.Block.GetBlockName())
			{
				case "trapSpikesWoodDmg1":
				case "trapSpikesWoodDmg2":
					block = new BlockValue(trapSpikesWoodDmg0_id);
					break;

				case "trapSpikesIronDmg1":
				case "trapSpikesIronDmg2":
					block = new BlockValue(trapSpikesIronDmg0_id);
					break;

				default:
					// Do nothing -> block = block...
					break;
			}

			float damage_perc = (float)block.damage / block.Block.MaxDamage;

			// Take the repair materials from the container
			if (NEEDS_MATERIAL)
				missing_items = TakeRepairMaterials(damage_perc, repair_items);

			block.damage = compute_damage(block, missing_items);

			// Log.Out("");

			// Update the block at the given position (very low-level function)
			// Note: with this function we can basically install a new block at position
			world.SetBlock(chunkFromWorldPos.ClrIdx, pos, block, false, false);

			// BroadCast the changes done to the block
			world.SetBlockRPC(
				chunkFromWorldPos.ClrIdx,
				pos,
				block,
				block.Block.Density
			);

			// Get material to play material specific sound
			var material = block.Block.blockMaterial.SurfaceCategory;
			world.GetGameManager().PlaySoundAtPositionServer(
				pos.ToVector3(),
				string.Format("ImpactSurface/metalhit{0}", material),
				AudioRolloffMode.Logarithmic, 100
			);

			// Update clients
			SetModified();
		}

		return missing_items;
	}

	private bool is_block_ignored(BlockValue block)
	{

		if (block.damage > 0)
			return false;

		return (
			block.isair
			|| block.isWater
			|| block.Block.shape.IsTerrain()
			//|| block.Block.IsDecoration
			|| block.Block.IsPlant()
			|| block.Block.IsTerrainDecoration
		);
	}

	private List<Vector3i> get_blocks_to_repair(World world, Vector3i initial_pos)
	{
		List<Vector3i> blocks_to_repair = new List<Vector3i>();
		List<Vector3i> neighbors = this.get_neighbors(initial_pos);
		Dictionary<string, int> visited = new Dictionary<string, int>();

		int iterations = MAX_ITERTIONS;

		while (neighbors.Count > 0 && iterations > 0)
		{
			iterations--;

			List<Vector3i> neighbors_temp = new List<Vector3i>(neighbors);
			neighbors = new List<Vector3i>();

			foreach (Vector3i pos in neighbors_temp)
			{
				BlockValue block = world.GetBlock(pos);

				bool is_ignored = this.is_block_ignored(block);
				bool is_visited = visited.ContainsKey(pos.ToString());

				if (!is_visited)
					visited.Add(pos.ToString(), 0);

				if (is_ignored || is_visited)
					continue;

				// allow to include damaged spike blocks
				string block_name = block.Block.GetBlockName();

				if (block.damage > 0 || block_name.Contains("Dmg1") || block_name.Contains("Dmg2"))
				{
					blocks_to_repair.Add(pos);
				}

				neighbors.AddRange(this.get_neighbors(pos));
			}
		}

		Log.Out($"{blocks_to_repair.Count} blocks to repair. Iterations = {MAX_ITERTIONS - iterations}, visited_blocks = {visited.Count}");

		return blocks_to_repair;
	}

	private void debug_block(World world, Vector3i block_pos)
	{
		BlockValue block = world.GetBlock(block_pos);

		Log.Out($"block.name .............. : {block.Block.GetBlockName()}");
		Log.Out($"block.pos ............... : [{block_pos.x}, {block_pos.y}, {block_pos.z}]");
		Log.Out($"block.type .............. : {block.type}");
		Log.Out($"block.damage ............ : {block.damage}");
		Log.Out($"block.isair ............. : {block.isair}");
		Log.Out($"block.isWater ........... : {block.isWater}");
		Log.Out($"block.IsDecoration ...... : {block.Block.IsDecoration}");
		Log.Out($"block.IsTerrain ......... : {block.Block.shape.IsTerrain()}");
		Log.Out($"block.IsPlant ........... : {block.Block.IsPlant()}");
		Log.Out($"block.IsTerrainDecoration : {block.Block.IsTerrainDecoration}");
		Log.Out($"block.IsDecoration ...... : {block.Block.IsDecoration}");
		Log.Out("");
	}

	private void debug_neighbors(World world, Vector3i position)
	{
		List<Vector3i> neighbors = get_neighbors(position);

		debug_block(world, neighbors[0]);
		debug_block(world, neighbors[1]);
		debug_block(world, neighbors[2]);
		debug_block(world, neighbors[3]);
		debug_block(world, neighbors[4]);
		debug_block(world, neighbors[5]);
	}

	public Dictionary<string, int> find_an_repair_damaged_blocks(World world)
	{
		Vector3i block_position = ToWorldPos();

		// debug_neighbors(world, block_position);

		List<Vector3i> blocks_to_repair = get_blocks_to_repair(world, block_position);

		Dictionary<string, int> missing_items = new Dictionary<string, int>();

		foreach (var position in blocks_to_repair)
		{
			Dictionary<string, int> block_missing_items = repair_block(world, position);

			if (block_missing_items == null)
				continue;

			foreach (KeyValuePair<string, int> entry in block_missing_items)
			{
				if (!missing_items.ContainsKey(entry.Key))
					missing_items.Add(entry.Key, 0);

				missing_items[entry.Key] += entry.Value;
			}
		}

		return missing_items;
	}

	public override void SetUserAccessing(bool _bUserAccessing)
	{
		if (IsUserAccessing() != _bUserAccessing)
		{
			base.SetUserAccessing(_bUserAccessing);
			if (_bUserAccessing)
			{
				if (lastMissingItem != null)
				{
					var player = GameManager.Instance?.World?.GetPrimaryPlayer();
					string msg = Localization.Get("ocbBlockClaimAutoRepairMissed");
					if (string.IsNullOrEmpty(msg)) msg = "Claim Auto Repair could use {0}";
					msg = string.Format(msg, ItemClass.GetItemClass(lastMissingItem).GetLocalizedItemName());
					GameManager.Instance.ChatMessageServer(
						(ClientInfo)null,
						EChatType.Whisper,
						player.entityId,
						msg,
						string.Empty, false,
						new List<int> { player.entityId });
					lastMissingItem = null;
				}

				//ResetAcquiredBlock("weapon_jam", false);
				SetModified(); // Force update
			}
		}
	}

	private void EnableBoundHelper(float progress = 0)
	{
		if (BoundsHelper == null) return;

		//BoundsHelper.localPosition = repairPosition.ToVector3() - Origin.position + new Vector3(0.5f, 0.5f, 0.5f);
		BoundsHelper.gameObject.SetActive(this.__isOn);

		Color color = Color.yellow * (1f - progress) + Color.green * progress;

		//if (lastColor == color) return;

		foreach (Renderer componentsInChild in BoundsHelper.GetComponentsInChildren<Renderer>())
			componentsInChild.material.SetColor("_Color", color * 0.5f);

		//lastColor = color;
	}

	private void ResetBoundHelper(Color color)
	{
		if (BoundsHelper == null)
			return;

		BoundsHelper.localPosition = ToWorldPos().ToVector3() - Origin.position + new Vector3(0.5f, 0.5f, 0.5f);
		BoundsHelper.gameObject.SetActive(this.__isOn);

		// Only update if necessary
		//if (lastColor == color) return;
		foreach (Renderer componentsInChild in BoundsHelper.GetComponentsInChildren<Renderer>())
			componentsInChild.material.SetColor("_Color", color * 0.5f);

		//lastColor = color;
	}

}
