#!/usr/bin/env python3
"""
🚀 TRAFO MERKEZİ MQTT SİMÜLATÖRÜ - YENİ PANEL YAPISI
"""

import paho.mqtt.client as mqtt
import random
import time
import json
import os
import sys
from datetime import datetime

print("=" * 60)
print("🏭 TRAFO MERKEZİ MQTT SİMÜLATÖRÜ - YENİ PANEL")
print("=" * 60)
print("")

# MQTT Broker bilgileri
broker = "localhost"  # veya "test.mosquitto.org"
port = 1883

# MQTT Client oluştur
print("🔗 MQTT Broker'a bağlanılıyor...")
# Satırı bu şekilde değiştirin:
client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION1, "Trafo_Panel_Simulator")
try:
    client.connect(broker, port)
    print("✅ MQTT Broker'a bağlandı!")
    print(f"   Host: {broker}:{port}")
except Exception as e:
    print(f"❌ Bağlantı hatası: {e}")
    print("\n🛠️  Lütfen şunları kontrol edin:")
    print("   1. Mosquitto çalışıyor mu?")
    print("   2. Port 1883 açık mı?")
    sys.exit(1)

print("\n🎯 YENİ PANEL VERİ FORMATI BAŞLATILDI")
print("   Topic'ler:")
print("     - sistem/yuk")
print("     - sistem/sicaklik") 
print("     - sistem/gerilim")
print("   Her 3 saniyede bir veri gönderilecek")
print("   Çıkmak için: Ctrl + C")
print("-" * 60)

# Başlangıç değerleri
yuk = 65.0
sicaklik = 45.0
gerilim = 54.0

# Simülasyon döngüsü
counter = 0
try:
    while True:
        counter += 1
        timestamp = datetime.now().strftime("%H:%M:%S")
        
        # 🔄 Değerleri yavaş yavaş değiştir (gerçekçi simülasyon)
        # Yük değeri (60-95 arası, bazen ani yükselmelerle)
        yuk_change = random.uniform(-3, 5)
        yuk = max(60, min(95, yuk + yuk_change))
        
        # Sıcaklık (yüke bağlı)
        temp_change = (yuk - 70) * 0.1 + random.uniform(-1, 1)
        sicaklik = max(40, min(85, sicaklik + temp_change))
        
        # Gerilim (nominal 54.0 KV, küçük dalgalanmalarla)
        volt_change = random.uniform(-0.5, 0.5)
        gerilim = max(52, min(56, 54.0 + volt_change))
        
        # 🎯 YENİ TOPİC YAPISI - AYRI AYRI GÖNDER
        # 1. YÜK verisi
        yuk_topic = "sistem/yuk"
        yuk_payload = f"{yuk:.1f}"
        client.publish(yuk_topic, yuk_payload)
        
        # 2. SICAKLIK verisi  
        sicaklik_topic = "sistem/sicaklik"
        sicaklik_payload = f"{sicaklik:.1f}"
        client.publish(sicaklik_topic, sicaklik_payload)
        
        # 3. GERİLİM verisi
        gerilim_topic = "sistem/gerilim"
        gerilim_payload = f"{gerilim:.1f}"
        client.publish(gerilim_topic, gerilim_payload)
        
        # 📊 Durum tespiti
        yuk_status = "YÜKSEK" if yuk > 80 else "NORMAL"
        
        if sicaklik > 70:
            sicaklik_status = "KRİTİK"
        elif sicaklik > 50:
            sicaklik_status = "UYARI"
        else:
            sicaklik_status = "NORMAL"
            
        gerilim_status = "YÜKSEK GERİLİM" if gerilim > 55 else "NORMAL"
        
        # 📈 JSON formatında özet (opsiyonel - monitoring için)
        summary_data = {
            "timestamp": timestamp,
            "counter": counter,
            "yuk": round(yuk, 1),
            "sicaklik": round(sicaklik, 1),
            "gerilim": round(gerilim, 1),
            "status_yuk": yuk_status,
            "status_sicaklik": sicaklik_status,
            "status_gerilim": gerilim_status
        }
        client.publish("sistem/summary", json.dumps(summary_data))
        
        # 📝 Konsol çıktısı
        print(f"\n[{timestamp}] #{counter} - VERİ GÖNDERİLDİ")
        print(f"   ⚡ YÜK      : {yuk:5.1f} %   [{yuk_status}]")
        print(f"   🌡️  SICAKLIK: {sicaklik:5.1f} °C  [{sicaklik_status}]")
        print(f"   🔌 GERİLİM : {gerilim:5.1f} KV  [{gerilim_status}]")
        print("   📤 Topic'ler: sistem/yuk, sistem/sicaklik, sistem/gerilim")
        print("-" * 60)
        
        time.sleep(3)  # 3 saniye bekle
        
except KeyboardInterrupt:
    print("\n\n⏹️  Simülasyon durduruldu")
    
finally:
    client.disconnect()
    print("🔌 MQTT bağlantısı kapatıldı")
    print("\n✅ YENİ PANEL İÇİN VERİ AKIŞI HAZIR!")
    print("   Şimdi backend/server.py'yi çalıştırın")
    print("   Sonra frontend/index.html'yi açın")