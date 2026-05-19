#!/usr/bin/env python3
"""
🚀 TRAFO MERKEZİ MQTT SİMÜLATÖRÜ - MAC
"""

import paho.mqtt.client as mqtt
import random
import time
import json
import os
import sys

print("=" * 60)
print("🍎 TRAFO MERKEZİ MQTT SİMÜLATÖRÜ")
print("=" * 60)
print("")

# MQTT Broker bilgileri
broker = "localhost"
port = 1883

# MQTT Client oluştur
print("🔗 MQTT Broker'a bağlanılıyor...")
client = mqtt.Client()

try:
    client.connect(broker, port)
    print("✅ MQTT Broker'a bağlandı!")
    print(f"   Host: {broker}:{port}")
except Exception as e:
    print(f"❌ Bağlantı hatası: {e}")
    print("\n🛠️  Lütfen şunları kontrol edin:")
    print("   1. Mosquitto çalışıyor mu? 'brew services list | grep mosquitto'")
    print("   2. Terminal'i yeniden açıp tekrar deneyin")
    sys.exit(1)

print("\n🎯 SIMÜLASYON BAŞLATILDI")
print("   Topic: TM1/Trafo/sensor")
print("   Her 2 saniyede bir veri gönderilecek")
print("   Çıkmak için: Ctrl + C")
print("-" * 50)

# Simülasyon döngüsü
counter = 0
try:
    while True:
        # Trafo verisi oluştur
        trafo_data = {
            "sicaklik": round(random.uniform(40.0, 85.0), 2),
            "yag_sicaklik": round(random.uniform(30.0, 75.0), 2),
            "gerilim_primer": round(random.uniform(154000, 160000), 1),
            "gerilim_sekonder": round(random.uniform(33000, 36000), 1),
            "akim_primer": round(random.uniform(100, 550), 1),
            "akim_sekonder": round(random.uniform(800, 2600), 1),
            "yuk_seviyesi": round(random.uniform(60, 95), 1)
        }
        
        # JSON formatında gönder
        


        client.publish("TM1/Trafo/sensor", json.dumps(trafo_data))
        
        counter += 1
        print(f"{counter}. 📤 Veri gönderildi")
        print(f"   🌡️  Sıcaklık: {trafo_data['sicaklik']}°C")
        print(f"   ⚡ Gerilim: {trafo_data['gerilim_primer']/1000:.1f} kV")
        print(f"   📊 Yük: {trafo_data['yuk_seviyesi']}%")
        print("-" * 40)
        
        time.sleep(2)  # 2 saniye bekle
        
except KeyboardInterrupt:
    print("\n\n👋 Simülasyon durduruldu")
    
finally:
    client.disconnect()
    print("✅ MQTT bağlantısı kapatıldı")
    print("\n🎉 TAMAMLANDI! Unity için veri akışı hazır.")