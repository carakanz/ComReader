using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MoveCube : MonoBehaviour
{
    public int index;
    // Start is called before the first frame update
    void Start()
    {
        reader = Reader.Reader.GetInstance();
    }

    // Update is called once per frame
    void Update()
    {
        var frames = reader.Take(64);
        Debug.Log($"Frame {index} is {frames.Last().channels[index]}");
        int max = frames.Select(frame => frame.channels[index]).Max();
        int min = frames.Select(frame => frame.channels[index]).Min();
        Vector3 position = transform.position;
        position.y = max - min;
        transform.position = position;
    }

    private Reader.Reader reader;
}
