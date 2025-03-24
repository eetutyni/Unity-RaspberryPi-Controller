import socket
import RPi.GPIO as GPIO
from sense_hat import SenseHat
from threading import Thread
import time

# Set up GPIO
GPIO.setmode(GPIO.BCM)
LED_PIN = 18
GPIO.setup(LED_PIN, GPIO.OUT)

# Set up Sense HAT
sense = SenseHat()
sense.clear()

def udp_discovery_server():
    udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    udp_socket.bind(('0.0.0.0', 5000))  # Listen on port 5000 for UDP

    print("UDP discovery server started...")

    while True:
        data, address = udp_socket.recvfrom(1024)
        if data.decode() == "DISCOVER_RASPBERRY_PI":
            print(f"Discovery request from {address}")
            udp_socket.sendto("RASPBERRY_PI_RESPONSE".encode(), address)

def tcp_command_server():
    tcp_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    tcp_socket.bind(('0.0.0.0', 5001))  # Listen on port 5001 for TCP
    tcp_socket.listen(5)  # Allow multiple connections

    print("TCP command server started...")

    while True:
        connection, address = tcp_socket.accept()
        print(f"Connected to {address}")

        Thread(target=handle_client, args=(connection,)).start()

def display_sensor_data(sensor_data):
    """Display sensor data on Sense HAT with visual formatting"""
    try:
        parts = sensor_data.split(',')
        display_data = []
        
        for part in parts:
            if part.startswith("TEMP:"):
                temp = float(part[5:])
                color = [255, 0, 0] if temp > 30 else [0, 255, 0]  # Red if hot, green if normal
                display_data.append((f"Temp: {temp:.1f}C", color))
            elif part.startswith("HUM:"):
                hum = float(part[4:])
                color = [0, 100, 255]  # Blue for humidity
                display_data.append((f"Hum: {hum:.1f}%", color))
            elif part.startswith("PRES:"):
                pres = float(part[5:])
                color = [255, 255, 0]  # Yellow for pressure
                display_data.append((f"Pres: {pres:.1f}hPa", color))
        
        # Display on Sense HAT
        sense.clear()
        sense.show_message("Remote Sensors:", scroll_speed=0.05, text_colour=[255, 255, 255])
        
        for message, color in display_data:
            sense.show_message(message, scroll_speed=0.05, text_colour=color)
            time.sleep(0.5)
            
    except Exception as e:
        print(f"Error displaying sensor data: {e}")
        sense.show_message("Data Error", scroll_speed=0.05, text_colour=[255, 0, 0])

def handle_client(connection):
    try:
        while True:
            data = connection.recv(1024).decode()
            if not data:
                break

            print(f"Received: {data}")

            if data == "LED_ON":
                GPIO.output(LED_PIN, GPIO.HIGH)
                sense.clear((0, 255, 0))  # Green
                sense.show_message("LED ON", scroll_speed=0.05, text_colour=[255, 255, 255])
                connection.send("LED turned ON".encode())
                
            elif data == "LED_OFF":
                GPIO.output(LED_PIN, GPIO.LOW)
                sense.clear()
                connection.send("LED turned OFF".encode())
                
            elif data == "GET_SENSOR_DATA":
                # Get current sensor readings
                temp = sense.get_temperature()
                humidity = sense.get_humidity()
                pressure = sense.get_pressure()
                
                # Format sensor data
                sensor_data = f"TEMP:{temp:.2f},HUM:{humidity:.2f},PRES:{pressure:.2f}"
                connection.send(sensor_data.encode())
                
            elif data.startswith("DISPLAY_SENSOR_DATA:"):
                # Extract and display sensor data from another Pi
                sensor_data = data.split(":", 1)[1]
                print(f"Displaying remote sensor data: {sensor_data}")
                connection.send("Displaying sensor data".encode())
                
                # Display the data on Sense HAT
                display_sensor_data(sensor_data)
                
            else:
                connection.send("Unknown command!".encode())
                
    except ConnectionResetError:
        print("Client disconnected.")
    except Exception as e:
        print(f"Error in client handler: {e}")
    finally:
        connection.close()

if __name__ == "__main__":
    try:
        # Start UDP and TCP servers in separate threads
        Thread(target=udp_discovery_server, daemon=True).start()
        Thread(target=tcp_command_server, daemon=True).start()
        
        # Show ready message
        sense.show_message("Ready", scroll_speed=0.05, text_colour=[0, 255, 0])
        
        # Keep main thread alive
        while True:
            time.sleep(1)
            
    except KeyboardInterrupt:
        print("\nShutting down...")
    finally:
        sense.clear()
        GPIO.cleanup()
