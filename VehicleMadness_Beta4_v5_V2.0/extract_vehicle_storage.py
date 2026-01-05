#!/usr/bin/env python3
"""
Vehicle Madness Storage Extractor for 7 Days to Die
Parses mod configuration files and extracts vehicle storage information.
"""

import os
import re
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_loot_containers(loot_xml_path):
    """Parse loot.xml to extract storage container sizes."""
    containers = {}
    
    # Default vanilla vehicle storage sizes (approximate - rows x cols)
    vanilla_defaults = {
        'vehicleMinibike': (6, 6, 36),      # 6x6 = 36 slots
        'vehicleMotorcycle': (8, 6, 48),    # 8x6 = 48 slots
        'vehicle4x4Truck': (9, 8, 72),      # 9x8 = 72 slots
        'vehicleGyrocopter': (9, 8, 72),    # 9x8 = 72 slots
    }
    containers.update({k: v for k, v in vanilla_defaults.items()})
    
    if os.path.exists(loot_xml_path):
        try:
            with open(loot_xml_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Find lootcontainer elements with size attribute
            pattern = r'<lootcontainer\s+name="([^"]+)"[^>]*size="(\d+),(\d+)"'
            matches = re.findall(pattern, content)
            
            for name, rows, cols in matches:
                rows, cols = int(rows), int(cols)
                total = rows * cols
                containers[name] = (rows, cols, total)
                
        except Exception as e:
            print(f"Warning: Could not parse loot.xml: {e}")
    
    return containers


def parse_entity_classes(entity_xml_path):
    """Parse entityclasses.xml to extract vehicle definitions."""
    vehicles = []
    
    if not os.path.exists(entity_xml_path):
        print(f"Error: {entity_xml_path} not found")
        return vehicles
    
    try:
        tree = ET.parse(entity_xml_path)
        root = tree.getroot()
        
        # Find all entity_class elements within append elements
        for append_elem in root.findall('.//append'):
            for entity_class in append_elem.findall('entity_class'):
                name = entity_class.get('name', '')
                extends = entity_class.get('extends', '')
                
                # Check if it's a vehicle (starts with 'vehicle')
                if not name.startswith('vehicle'):
                    continue
                
                # Get LootListAlive property if it exists
                loot_list = None
                for prop in entity_class.findall('property'):
                    if prop.get('name') == 'LootListAlive':
                        loot_list = prop.get('value')
                        break
                
                # Get Tags for display name hints
                tags = ''
                for prop in entity_class.findall('property'):
                    if prop.get('name') == 'Tags':
                        tags = prop.get('value', '')
                        break
                
                vehicles.append({
                    'name': name,
                    'extends': extends,
                    'loot_list': loot_list,
                    'tags': tags
                })
                
    except ET.ParseError as e:
        print(f"Error parsing entityclasses.xml: {e}")
    
    return vehicles


def resolve_storage(vehicles, containers):
    """Resolve storage size for each vehicle."""
    # Build a lookup for inheritance
    vehicle_lookup = {v['name']: v for v in vehicles}
    
    results = []
    for vehicle in vehicles:
        name = vehicle['name']
        loot_list = vehicle['loot_list']
        
        # If no loot_list, try to inherit from parent
        if not loot_list:
            parent = vehicle.get('extends')
            while parent and parent in vehicle_lookup:
                parent_vehicle = vehicle_lookup[parent]
                if parent_vehicle.get('loot_list'):
                    loot_list = parent_vehicle['loot_list']
                    break
                parent = parent_vehicle.get('extends')
        
        # Default to vehicle4x4Truck if still not found
        if not loot_list:
            loot_list = 'vehicle4x4Truck'
        
        # Get container size
        if loot_list in containers:
            rows, cols, total = containers[loot_list]
        else:
            rows, cols, total = (9, 8, 72)  # Default fallback
        
        # Format display name from vehicle name
        display_name = name.replace('vehicleVM', 'VM ').replace('vehicle', '')
        
        results.append({
            'name': name,
            'display_name': display_name,
            'loot_container': loot_list,
            'rows': rows,
            'cols': cols,
            'total_slots': total
        })
    
    return results


def generate_output(results, output_path):
    """Generate formatted output file."""
    # Sort by total slots (descending) then by name
    results.sort(key=lambda x: (-x['total_slots'], x['display_name']))
    
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("=" * 70 + "\n")
        f.write("     VEHICLE MADNESS - VEHICLE STORAGE CAPACITY LIST\n")
        f.write("=" * 70 + "\n\n")
        f.write(f"{'Vehicle Name':<35} {'Storage Size':<15} {'Total Slots'}\n")
        f.write("-" * 70 + "\n")
        
        for v in results:
            size_str = f"{v['rows']}x{v['cols']}"
            f.write(f"{v['display_name']:<35} {size_str:<15} {v['total_slots']} slots\n")
        
        f.write("\n" + "-" * 70 + "\n")
        f.write(f"Total Vehicles: {len(results)}\n\n")
        
        # Group summary
        f.write("\nSTORAGE CATEGORY SUMMARY:\n")
        f.write("-" * 35 + "\n")
        
        categories = {}
        for v in results:
            key = v['loot_container']
            if key not in categories:
                categories[key] = {'count': 0, 'slots': v['total_slots']}
            categories[key]['count'] += 1
        
        for cat, info in sorted(categories.items(), key=lambda x: -x[1]['slots']):
            f.write(f"{cat:<25} {info['count']:>3} vehicles ({info['slots']} slots each)\n")
        
        f.write("\n" + "=" * 70 + "\n")
        f.write("Generated by Vehicle Madness Storage Extractor\n")
    
    print(f"Output saved to: {output_path}")


def main():
    # Get script directory (should be in the mod folder)
    script_dir = Path(__file__).parent
    config_dir = script_dir / "Config"
    
    # Parse files
    print("Parsing Vehicle Madness configuration files...\n")
    
    loot_xml = config_dir / "loot.xml"
    entity_xml = config_dir / "entityclasses.xml"
    
    containers = parse_loot_containers(str(loot_xml))
    print(f"Found {len(containers)} loot container definitions")
    
    vehicles = parse_entity_classes(str(entity_xml))
    print(f"Found {len(vehicles)} vehicle definitions")
    
    # Resolve storage sizes
    results = resolve_storage(vehicles, containers)
    
    # Generate output
    output_path = script_dir / "vehicle_storage_list.txt"
    generate_output(results, str(output_path))
    
    print("\nDone!")


if __name__ == "__main__":
    main()

