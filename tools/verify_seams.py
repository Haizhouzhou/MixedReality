"""Independently prove that source open edges survive every exported tile LOD.

Centroid partition boundaries are zig-zags, so a modulo-grid test is insufficient.
Reads one source chunk and one GLB at a time; never touches the original dataset.
"""
import argparse
import json
import hashlib
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
import trimesh


def source_edges(root, tile):
    with np.load(root / 'chunks' / (tile + '.npz')) as data:
        p, f = data['positions'], data['faces']
    valid = (f[:, 0] != f[:, 1]) & (f[:, 1] != f[:, 2]) & (f[:, 0] != f[:, 2])
    f = f[valid]
    cross = np.cross(p[f[:, 1]]-p[f[:, 0]], p[f[:, 2]]-p[f[:, 0]])
    f = f[(cross*cross).sum(axis=1) > 1e-16]
    canonical=np.ascontiguousarray(np.sort(f,axis=1))
    _, unique = np.unique(canonical.view(np.dtype((np.void,canonical.dtype.itemsize*3))).ravel(),return_index=True)
    f = f[unique]
    edges = np.sort(np.concatenate((f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]])), axis=1)
    # Packed integer keys avoid expensive row-wise lexicographic sorting of
    # millions of edge pairs. uint64 is sufficient for the bounded chunk size.
    keys=edges[:,0].astype(np.uint64)*np.uint64(len(p))+edges[:,1]
    keys,counts=np.unique(keys,return_counts=True);keys=keys[counts==1]
    edges=np.column_stack((keys//np.uint64(len(p)),keys%np.uint64(len(p)))).astype(np.int64)
    ids, inverse = np.unique(edges, return_inverse=True)
    return p[ids], inverse.reshape(-1, 2)


def verify(root, only=None, report_only=False):
    root = Path(root); manifest = json.loads((root/'manifest.json').read_text())
    results = []; origin = np.array(manifest['origin_world'])
    for tile in manifest['tiles']:
        if only and tile['id'] != only:
            continue
        boundary, edges = source_edges(root, tile['id'])
        for level, filename in tile['files'].items():
            mesh = trimesh.load(root/filename, force='scene', process=False).to_geometry()
            p = np.column_stack((mesh.vertices[:,0], -mesh.vertices[:,2], mesh.vertices[:,1])) + origin
            distances, indices = cKDTree(p).query(boundary)
            error = float(distances.max(initial=0))
            output_edges = np.sort(np.concatenate((mesh.faces[:,[0,1]],mesh.faces[:,[1,2]],mesh.faces[:,[2,0]])),axis=1).astype(np.uint64)
            wanted = np.sort(indices[edges],axis=1).astype(np.uint64)
            keys = output_edges[:,0]*np.uint64(len(p))+output_edges[:,1]
            wanted_keys = wanted[:,0]*np.uint64(len(p))+wanted[:,1]
            missing = int((~np.isin(wanted_keys,keys)).sum())
            item = dict(tile=tile['id'],lod=level,source_boundary_vertices=len(boundary),source_boundary_edges=len(edges),max_displacement_m=error,missing_edges=missing,glb_sha256=hashlib.sha256((root/filename).read_bytes()).hexdigest())
            results.append(item); print(json.dumps(item),flush=True)
            if not report_only:
                assert error < 2e-5 and missing == 0, f"Seam changed: {item}"
    if not report_only:
        (root/'seams.json').write_text(json.dumps({'policy':'exact-open-boundary-v1','results':results},indent=2))
        print(f'PASS: {len(results)} tile LODs preserve all source open edges',flush=True)
    return results


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('out',type=Path);parser.add_argument('--tile');parser.add_argument('--report-only',action='store_true')
    args=parser.parse_args();verify(args.out,args.tile,args.report_only)
