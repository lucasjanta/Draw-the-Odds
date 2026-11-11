using System.Collections.Generic;
using UnityEngine;

// Monta as plataformas de cada fase clonando um "template" (a plataforma one-way da cena).
// C# puro: o GameManager cria e chama Build().
public class LevelBuilder
{
    public const float FloorTop = -7.5f;     // o chão ocupa a faixa de baixo da tela (onde fica a mão)
    private const float PlatformHeight = 0.5f;

    // (x do centro, y do centro, largura) de cada plataforma, por layout.
    private static readonly Vector3[][] Layouts =
    {
        new[] { new Vector3(-11f, -3f, 10f), new Vector3(11f, -3f, 10f), new Vector3(0f, 1.25f, 12f),
                new Vector3(-12f, 5.5f, 8f), new Vector3(12f, 5.5f, 8f) },
        new[] { new Vector3(-14f, -3f, 8f), new Vector3(0f, -3f, 8f), new Vector3(14f, -3f, 8f),
                new Vector3(-7f, 1.25f, 8f), new Vector3(7f, 1.25f, 8f), new Vector3(0f, 5.5f, 10f) },
        new[] { new Vector3(0f, -3f, 14f), new Vector3(-13f, 1.25f, 10f), new Vector3(13f, 1.25f, 10f),
                new Vector3(-15f, 5.5f, 6f), new Vector3(0f, 5.5f, 6f), new Vector3(15f, 5.5f, 6f) },
    };

    // Superfícies onde dá para spawnar pickups: x mínimo, x máximo, y do topo.
    public struct Surface { public float minX, maxX, top; }

    private readonly Transform template;
    private readonly SpriteRenderer floor;
    private readonly Transform root;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Surface> surfaces = new List<Surface>();

    public IReadOnlyList<Surface> Surfaces => surfaces;

    public LevelBuilder(Transform template, SpriteRenderer floor)
    {
        this.template = template;
        this.floor = floor;
        root = new GameObject("Generated Platforms").transform;
        if (template != null) template.gameObject.SetActive(false);
    }

    public void Build(int layoutIndex, Color platformColor)
    {
        foreach (var go in spawned) Object.Destroy(go);
        spawned.Clear();
        surfaces.Clear();

        if (floor != null) floor.color = Color.Lerp(platformColor, Color.black, 0.45f);
        surfaces.Add(new Surface { minX = -18f, maxX = 18f, top = FloorTop });

        var layout = Layouts[Mathf.Clamp(layoutIndex, 0, Layouts.Length - 1)];
        foreach (var p in layout)
        {
            var go = Object.Instantiate(template.gameObject, root);
            go.name = "Platform";
            go.SetActive(true);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            go.transform.localScale = new Vector3(p.z, PlatformHeight, 1f);
            if (go.TryGetComponent(out SpriteRenderer sr)) sr.color = platformColor;
            spawned.Add(go);
            surfaces.Add(new Surface { minX = p.x - p.z / 2f + 1f, maxX = p.x + p.z / 2f - 1f, top = p.y + PlatformHeight / 2f });
        }
    }

    // Ponto aleatório em cima de alguma superfície, longe do ponto "avoid".
    public Vector2 RandomSpawnPoint(Vector2 avoid, float minDistance)
    {
        Vector2 best = Vector2.zero;
        float bestDist = -1f;
        for (int i = 0; i < 12; i++)
        {
            var s = surfaces[Random.Range(0, surfaces.Count)];
            var p = new Vector2(Random.Range(s.minX, s.maxX), s.top + 0.9f);
            float d = Vector2.Distance(p, avoid);
            if (d >= minDistance) return p;
            if (d > bestDist) { bestDist = d; best = p; }
        }
        return best;
    }
}
