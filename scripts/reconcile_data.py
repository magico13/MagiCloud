# python file to remove items from the elastic search index that don't exist on the file system
# and to remove items from the file system that don't exist in the elastic search index

import os
from elasticsearch import Elasticsearch

# Set up Elasticsearch client
es = Elasticsearch('https://localhost:9200', basic_auth=('elastic', 'password'), verify_certs=False)  # replace with your Elasticsearch info

# Fetch objects from Elasticsearch
res = es.search(index="magicloud_files",  query={"match_all": {}}, size=10000)

# List of all files in elasticsearch
es_files = []
for hit in res['hits']['hits']:
    es_files.append(hit['_source']['id'])

# List of all files in the file system
fs_files = []
for root, dirs, files in os.walk('data'):
    for name in files:
        fs_files.append(name)

# List of files in elasticsearch but not in the file system
es_not_fs = list(set(es_files) - set(fs_files))

# List of files in the file system but not in elasticsearch
fs_not_es = list(set(fs_files) - set(es_files))

# for now just print out the two lists
print('Files in elasticsearch but not in the file system:')
print(es_not_fs)

print('Files in the file system but not in elasticsearch:')
print(fs_not_es)

# when they press enter, delete them
input('Press enter to delete files in elasticsearch but not in the file system')

for file in es_not_fs:
    es.delete(index="magicloud_files", id=file)

input('Press enter to delete files in the file system but not in elasticsearch')

for file in fs_not_es:
    os.remove('data/' + file)

print('Done')
