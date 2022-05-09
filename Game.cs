using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

enum BlockFace {
	TOP,
	BOTTOM,
	LEFT,
	RIGHT,
	FRONT,
	BACK
}

class Chunk {
	public int x;
	public int z;
	public Block[,,] blocks = new Block[16,256,16];
	public CollisionShape collider;
	public MeshInstance meshInstance;

}

class Block {
	public Block(int id, bool transparent) {
		this.id = id;
		this.transparent = transparent;
	}
	int id;
	bool transparent;
	public bool Transparent {get { return transparent; } }
	public int Id { get { return id; } }
}

struct FaceDrawing {
	public int x;
	public int y;
	public int z;
	public BlockFace face;
	public FaceDrawing(int x, int y, int z, int id, BlockFace face) {
		this.x = x;
		this.y = y;
		this.z = z;
		this.face = face;
	}
}

public class Game : Spatial
{
	const float UVPERBLOCK = 0.0625f;
	const int RENDERDISTANCE = 8;
	Dictionary<Tuple<int,int>,Chunk> chunks = new Dictionary<Tuple<int, int>, Chunk>();
	[Export] Texture grassTexture;
	[Export] Godot.Collections.Array<Material> blockMaterials;
	[Export] NodePath playerPath;
	Player player;
	OpenSimplexNoise noise;
	Dictionary<Tuple<int,int>,List<MeshInstance>> chunkMeshes = new Dictionary<Tuple<int, int>, List<MeshInstance>>();
	Dictionary<Tuple<int,int>,List<CollisionShape>> chunkColliders = new Dictionary<Tuple<int, int>, List<CollisionShape>>();

	public override void _Ready() {
		player = GetNode<Player>(playerPath);
		CreateMap();
		Godot.Timer timer = new Godot.Timer();
		AddChild(timer);
		timer.Start(0.5f);
		timer.Connect("timeout",this,"CheckForLoading");
	}
	public override void _Process(float delta) {
		if(Input.IsActionJustPressed("ui_accept")) {
			CreateMap();
		}
	}
	private void CheckForLoading() {
		int x = (int)player.GlobalTransform.origin.x/16*16;
		int z = (int)player.GlobalTransform.origin.z/16*16;
		for(int r = 0; r < RENDERDISTANCE; r++) {
			int n = r*2 + 1;
			for(int i = -r; i <= r; i++ ) {
				for(int j = -r; j <= r; j++) {
					if(i != -r && j != -r || i != r && j != r) continue;
					Tuple<int,int> c = new Tuple<int,int>(x+i*16,z+j*16);
					if(!chunks.ContainsKey(c)) {
						LoadChunk(c);
					}
				}
			}
		}
	}
	private void LoadChunk(Tuple<int,int> c) {
		Chunk chunk = new Chunk();
		chunk.x = c.Item1;
		chunk.z = c.Item2;
		Block[,,] blocks = chunk.blocks;
		for (int x = 0; x < 16; x++ ) 
			for (int y = 0; y < 256; y++) 
				for (int z = 0; z < 16; z++) {
					double a = noise.GetNoise2d((chunk.x+x)*3,(chunk.z+z)*6)*72.0+24.0;
					if (y < a) {
						blocks[x, y, z] = new Block(2 + 2*Convert.ToInt32(a-y < 4), false);
					} else {
						blocks[x, y, z] = new Block(0, true);
					}

				}
			
		chunk.blocks = blocks;
		chunks.Add(c,chunk);
	}
	private void UnloadChunk(Tuple<int,int> c) {
		Chunk chunk = chunks[c];
		chunk.collider.QueueFree();
		chunk.meshInstance.QueueFree(); 
		chunks.Remove(c);
	}
	int ChunkCount = 0;
	private void CreateMap() {
		List<Tuple<int,int>> chunksToUnload = new List<Tuple<int, int>>();
		foreach(Tuple<int,int> c in chunks.Keys) chunksToUnload.Add(c);
		foreach(Tuple<int,int> c in chunksToUnload) UnloadChunk(c);
		foreach(List<MeshInstance> l in chunkMeshes.Values) foreach(MeshInstance meshInstance in l) meshInstance.QueueFree();
		foreach(List<CollisionShape> l in chunkColliders.Values) foreach(CollisionShape collider in l) collider.QueueFree();
		chunkMeshes.Clear();
		chunkColliders.Clear();
		chunks.Clear();
		ChunkCount = 64;
		noise = new OpenSimplexNoise();
		noise.Seed = (int)GD.Randi();
		for(int i = 0; i < 12; i++) {
			for(int j = 0; j < 12; j++) {
				Tuple<int,int> c = new Tuple<int, int>(i*16,j*16);
				LoadChunk(c);
			}
		}
		foreach(Chunk chunk in chunks.Values) {
			DrawChunk(chunk);
		}
	}
	

	private void DrawChunk(Chunk chunk) {
		SurfaceTool s = new SurfaceTool();
		s.Begin(Mesh.PrimitiveType.Triangles);
		Block[,,] blocks = chunk.blocks;
		int chunkX = chunk.x;
		int chunkZ = chunk.z;
		Dictionary<int,List<FaceDrawing>> facesToDraw = new Dictionary<int,List<FaceDrawing>>();
		ArrayMesh mesh = new ArrayMesh();
		MeshInstance meshInstance = new MeshInstance();
		for(int x = 0; x < 16 ; x++) {
			for(int y = 0 ; y < 256 ; y++) {
				for(int z = 0; z < 16 ; z++) {
					if(blocks[x,y,z].Id == 0) continue;
					if(!facesToDraw.ContainsKey(blocks[x,y,z].Id)) {
						facesToDraw.Add(blocks[x,y,z].Id, new List<FaceDrawing>());
					}
					List<FaceDrawing> ftd = facesToDraw[blocks[x,y,z].Id];
					int fx = chunkX + x;
					int fz = chunkZ + z;
					int id = blocks[x,y,z].Id;
					if(x > 0 && blocks[x-1,y,z].Transparent || x == 0 && CheckChunk(15,y,z,chunkX-16,chunkZ)) AddFace(s, new Vector3(fx,y,fz),BlockFace.LEFT,id);
					if(y > 0 && blocks[x,y-1,z].Transparent || y == 0) AddFace(s, new Vector3(fx,y,fz),BlockFace.BOTTOM,id);
					if(z > 0 && blocks[x,y,z-1].Transparent || z == 0 && CheckChunk(x,y,15,chunkX, chunkZ-16)) AddFace(s, new Vector3(fx,y,fz),BlockFace.FRONT,id);                   
					if(z < 15 && blocks[x,y,z+1].Transparent || z == 15 && CheckChunk(x,y,0,chunkX, chunkZ+16)) AddFace(s, new Vector3(fx,y,fz),BlockFace.BACK,id);
					if(y < 255 && blocks[x,y+1,z].Transparent || y == 255) AddFace(s, new Vector3(fx,y,fz),BlockFace.TOP,id);
					if(x < 15 && blocks[x+1,y,z].Transparent || x == 15 && CheckChunk(0,y,z,chunkX+16, chunkZ)) AddFace(s, new Vector3(fx,y,fz),BlockFace.RIGHT,id);
				}
			}
		}
		s.SetMaterial(blockMaterials[0]);
		s.Commit(mesh);
		meshInstance.Mesh = mesh;
		Shape shape = mesh.CreateTrimeshShape();
		CollisionShape collisionShape = new CollisionShape();
		collisionShape.Shape = shape;
		chunk.meshInstance = meshInstance;
		chunk.collider = collisionShape;
		GetNode("Terrain").AddChild(collisionShape);
		AddChild(meshInstance);
	}

	private bool CheckChunk(int x, int y, int z, int chunkX, int chunkZ) {
		if(chunks.ContainsKey(new Tuple<int,int>(chunkX,chunkZ))) {
			return chunks[new Tuple<int,int>(chunkX,chunkZ)].blocks[x,y,z].Transparent;
		} else {
			return false;
		}
	}

	private void AddVertex(SurfaceTool s, Vector3 pos, Vector3 normal, Vector2 uv, int blockId) {
		s.AddNormal(normal);
		s.AddUv(uv*UVPERBLOCK+new Vector2(blockId/16.0f,Mathf.PosMod(blockId,16.0f)));
		s.AddVertex(pos);
	}


	private void AddFace(SurfaceTool s, Vector3 blockCoordinates, BlockFace face, int blockId) {
		switch(face) {
			case BlockFace.TOP:
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,1,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,1,0), new Vector2(1,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,1,0), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,1,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,1,0), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,1,0), new Vector2(0,1),blockId);
				break;
			case BlockFace.BOTTOM:
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,-1,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,-1,0), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(0,-1,0), new Vector2(1,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,-1,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,-1,0), new Vector2(0,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,-1,0), new Vector2(1,1),blockId);
				break;
			case BlockFace.LEFT:
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(-1,0,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(-1,0,0), new Vector2(1,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(-1,0,0), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(-1,0,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(-1,0,0), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(-1,0,0), new Vector2(0,1),blockId);
				break;
			case BlockFace.RIGHT:
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(1,0,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(1,0,0), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(1,0,0), new Vector2(1,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(1,0,0), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(1,0,0), new Vector2(0,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(1,0,0), new Vector2(1,1),blockId);
				break;
			case BlockFace.FRONT:
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,0,1), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z), new Vector3(0,0,1), new Vector2(1,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,0,1), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z), new Vector3(0,0,1), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,0,1), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z), new Vector3(0,0,1), new Vector2(0,1),blockId);
				break;
			case BlockFace.BACK:
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(1,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(1,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(0,0),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(0,1),blockId);
				AddVertex(s,new Vector3(blockCoordinates.x + 1, blockCoordinates.y + 1, blockCoordinates.z + 1), new Vector3(0,0,-1), new Vector2(1,1),blockId);
				break;
		}
	}

}
