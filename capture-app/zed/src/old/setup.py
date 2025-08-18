from setuptools import setup

setup(
    name="plerion_capture_zed",
    version="0.1.0",
    python_requires=">=3.8",
    install_requires=[
        "cython",
        "pyopengl",
        "requests",
        "numpy>=1.23,<1.27",
        "scipy",
        "opencv-python",
    ],
    extras_require={
        "types": [
            "scipy-stubs",
        ],
    },
)