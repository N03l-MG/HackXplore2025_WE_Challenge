import os
import sys
import json
from dotenv import load_dotenv
from googleapiclient.discovery import build

def search_component(part_number):
    load_dotenv()
    api_key = os.getenv('GOOGLE_API_KEY')
    cse_id = os.getenv('GOOGLE_CSE_ID')

    service = build('customsearch', 'v1', developerKey=api_key)
    
    try:
        result = service.cse().list(q=part_number, cx=cse_id).execute()
        return json.dumps(result)
    except Exception as e:
        return json.dumps({"error": str(e)})

if __name__ == "__main__":
    if len(sys.argv) > 1:
        print(search_component(sys.argv[1]))
