import xml.etree.ElementTree as ET
import os
import re

file_path = r"Config/progression.xml"

if not os.path.exists(file_path):
    print(f"File not found: {file_path}")
    exit(1)

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Remove XML declaration if present
content = re.sub(r'<\?xml.*?\?>', '', content)
wrapped_content = f"<root>{content}</root>"

try:
    root = ET.fromstring(wrapped_content)
except ET.ParseError as e:
    print(f"XML Parse Error: {e}")
    exit(1)

risky_passives = [
    "HealthMax", "StaminaMax", "CarryCapacity", 
    "WalkSpeed", "RunSpeed", "CrouchSpeed",
    "FoodLoss", "WaterLoss", "HealthChangeOT", "StaminaChangeOT",
    "HyperthermalResist", "HypothermalResist",
    "BuffResistance", "DamageModifier", "AttributeLevel"
]

print("Scanning for ItemHasTags combined with risky passive effects...")

def check_element(elem, path=""):
    # Look for effect_groups
    for child in elem:
        if child.tag == "effect_group":
            analyze_effect_group(child, path)
        else:
            new_path = f"{path}/{child.tag}"
            if 'name' in child.attrib:
                new_path += f"[@name='{child.attrib['name']}']"
            check_element(child, new_path)

def analyze_effect_group(eg, path):
    has_item_tags = False
    item_tag_req = None
    
    # Check requirements in this group
    for req in eg.findall("requirement"):
        if req.attrib.get("name") == "ItemHasTags":
            has_item_tags = True
            item_tag_req = req
            break
            
    if not has_item_tags:
        return

    # Check passives
    for passive in eg.findall("passive_effect"):
        p_name = passive.attrib.get("name")
        if any(rp in p_name for rp in risky_passives):
            print(f"POTENTIAL CRASH: Found ItemHasTags with {p_name}")
            print(f"  Location: {path}")
            print(f"  Requirement: {ET.tostring(item_tag_req, encoding='unicode').strip()}")
            print(f"  Passive: {ET.tostring(passive, encoding='unicode').strip()}")
            print("-" * 40)

check_element(root)
