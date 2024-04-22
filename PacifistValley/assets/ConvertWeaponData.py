# Tool to convert old weapon data fromat to the new one
# This script was written by ChatGPT with some modifications from me
import json

def reformat_data(input_file, output_file):
    with open(input_file, 'r') as f:
        data = json.load(f)
    
    reformatted_data = {}
    
    for key, value in data.items():
        parts = value.split('/')
        parts.insert(0, "")
        
        reformatted_data[key] = {
            "Name": parts[1],
            "DisplayName": parts[1],
            "Description": parts[2],
            "MinDamage": int(parts[3]),
            "MaxDamage": int(parts[4]),
            "Knockback": float(parts[5]) if len(parts) > 5 else 1,
            "Speed": int(parts[6]) if len(parts) > 6 else 0,
            "Precision": int(parts[7]) if len(parts) > 7 else 0,
            "Defense": int(parts[8]) if len(parts) > 8 else 0,
            "Type": int(parts[9]) if len(parts) > 9 else 1,
            "MineBaseLevel": int(parts[10]) if len(parts) > 10 else -1,
            "MineMinLevel": int(parts[11]) if len(parts) > 11 else -1,
            "AreaOfEffect": int(parts[12]) if len(parts) > 12 else 0,
            "CritChance": float(parts[13]) if len(parts) > 13 else 0.02,
            "CritMultiplier": float(parts[14]) if len(parts) > 14 else 3,
            "CanBeLostOnDeath": bool(parts[15]) if len(parts) > 15 else True,
            "Texture": parts[16] if len(parts) > 16 else "TileSheets/weapons",
            "SpriteIndex": int(parts[17]) if len(parts) > 17 else int(key),
            "Projectiles": parts[18] if len(parts) > 18 else None,
            "CustomFields": parts[19] if len(parts) > 19 else None
        }
    
    with open(output_file, 'w') as f:
        json.dump(reformatted_data, f, indent=4)

# Replace 'input.json' and 'output.json' with your input and output file paths
reformat_data('old.json', 'new_weapon_data.json')
