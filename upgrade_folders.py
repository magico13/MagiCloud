from elasticsearch import Elasticsearch
from datetime import datetime
from pytz import timezone

# Set up Elasticsearch client
es = Elasticsearch('https://localhost:9200', basic_auth=('elastic', 'password'), verify_certs=False)  # replace with your Elasticsearch info

# Fetch objects from Elasticsearch
res = es.search(index="magicloud_files",  query={"match_all": {}}, size=10000)

# Dict to store folders and their IDs
folders_dict = {}

for hit in res['hits']['hits']:
    path = hit['_source']['name']
    path_parts = path.split('/')

    # print out path
    print('Working on file: '+ path)

    # Parent ID for folders
    parent_id = None

    # Iterate over directories in the path
    for dir_name in path_parts[:-1]:  # excluding the last part, which is the filename
        if dir_name and dir_name not in folders_dict:  # check that dir_name is not an empty string
            # Create new folder
            folder = {
                "name": dir_name,
                "parentId": parent_id,
                "userId": hit['_source']['userId'],
                "lastUpdated": datetime.now(timezone('US/Eastern')).isoformat(),
                "isPublic": False,
                "isDeleted": False
            }

            # Store folder in Elasticsearch
            res = es.index(index="magicloud_folders", document=folder)

            # Update folders_dict
            folders_dict[dir_name] = res['_id']

            # print out that folder was created
            print("Created folder: " + dir_name)

        # Update parent ID for next iteration
        parent_id = folders_dict.get(dir_name)

    # Create file
    file = {
        "name": path_parts[-1],  # filename
        "parentId": parent_id,
        "extension": hit['_source'].get('extension', None),
        "size": hit['_source'].get('size', None),
        "isDeleted": hit['_source'].get('isDeleted', False),
        "isPublic": hit['_source'].get('isPublic', False),
        "id": hit['_source']['id'],
        "mimeType": hit['_source'].get('mimeType', None),
        "lastModified": hit['_source'].get('lastModified', datetime.now(timezone('US/Eastern')).isoformat()),
        "lastUpdated": datetime.now(timezone('US/Eastern')).isoformat(),
        "text": hit['_source'].get('text', None),
        "userId": hit['_source']['userId'],
        "hash": hit['_source'].get('hash', None)
    }

    # Store file in Elasticsearch
    es.index(index="magicloud_files", id=hit['_id'], document=file)
