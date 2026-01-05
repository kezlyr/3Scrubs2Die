import re
from pathlib import Path

recipes_path = Path(r"d:\SteamLibrary\steamapps\common\7 Days To Die\Mods\ZZ_PerkOverhaul\Config\recipes.xml")

with open(recipes_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find all ingredients starting with resourceFood
ingredients = sorted(list(set(re.findall(r'<ingredient name="(resourceFood[^"]+)"', content))))

print(f"Found {len(ingredients)} missing ingredients:")
for ing in ingredients:
    print(ing)
